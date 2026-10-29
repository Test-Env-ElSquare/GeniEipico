using Domain.Entities.IoT;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Domain.Entities.Planning;
using Domain.Entities.Scada;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Integration;

/// <summary>
/// Synchronizes the populated Eipico definition tables into the existing Genie
/// master-data model. It intentionally does not copy Eipico's calculated tables
/// or views; those are rebuilt from Genie projections in a separate step.
/// </summary>
internal sealed class EipicoDefinitionSyncService(
    MesDbContext db,
    EipicoDefinitionSourceReader source,
    IOptions<EipicoSyncOptions> configuredOptions,
    ILogger<EipicoDefinitionSyncService> logger)
{
    private readonly EipicoSyncOptions options = configuredOptions.Value;

    public async Task<EipicoDefinitionSyncResult> SyncAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await source.ReadSnapshotAsync(cancellationToken);
        var executionStrategy = db.Database.CreateExecutionStrategy();
        var result = new EipicoDefinitionSyncResult();

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                db.ChangeTracker.Clear();
                var customer = await EnsureCustomerAsync(cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                var factories = await SyncFactoriesAsync(
                    customer.Id,
                    snapshot,
                    cancellationToken);
                result.Factories = factories.Count;
                await db.SaveChangesAsync(cancellationToken);

                await SyncDefaultFactoryShiftsAsync(
                    factories.Values,
                    cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                var lines = await SyncLinesAsync(
                    factories,
                    snapshot,
                    result,
                    cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                await SyncMachinesAsync(lines, snapshot, result, cancellationToken);
                await SyncMaterialsAsync(customer, snapshot, result, cancellationToken);
                await SyncSkusAsync(customer, snapshot, result, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                await SyncScalesAsync(factories, snapshot, result, cancellationToken);
                var transformers = await SyncTransformersAsync(
                    factories,
                    snapshot,
                    result,
                    cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                await SyncLineTransformersAsync(
                    lines,
                    transformers,
                    snapshot,
                    result,
                    cancellationToken);
                await SyncTagsAsync(snapshot, result, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        });

        logger.LogInformation(
            "Synchronized Eipico definitions: {Factories} factories, {Lines} lines, " +
            "{Machines} machines, {Materials} materials, {Skus} SKUs, {Scales} scales, " +
            "{Transformers} transformers, {LineTransformers} line-transformer links and {Tags} tags.",
            result.Factories,
            result.Lines,
            result.Machines,
            result.Materials,
            result.Skus,
            result.Scales,
            result.Transformers,
            result.LineTransformers,
            result.Tags);

        return result;
    }

    private async Task<Customer> EnsureCustomerAsync(CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .IgnoreQueryFilters()
            .Where(x => x.Code == options.CustomerCode)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                Code = Required(options.CustomerCode, 30),
                Name = TruncateRequired(options.CustomerName, 150),
                Tier = "enterprise",
                CountryCode = "EGY",
                Industry = "Pharmaceutical",
                ErpSystem = "SAP",
                IsActive = true
            };
            db.Customers.Add(customer);
        }
        else
        {
            Restore(customer);
            customer.Name = TruncateRequired(options.CustomerName, 150);
            customer.IsActive = true;
        }

        return customer;
    }

    private async Task<Dictionary<int, Factory>> SyncFactoriesAsync(
        int customerId,
        EipicoDefinitionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var sourceFactories = snapshot.Factories
            .OrderBy(x => x.Id)
            .ToList();

        // Keep the configured code for the first source factory so existing
        // realtime/SAP rows remain attached to the same target factory. Any
        // additional source factory receives a deterministic code based on its
        // Eipico definition id.
        if (sourceFactories.Count == 0)
        {
            sourceFactories.Add(new EipicoFactoryDefinition
            {
                Id = 0,
                Name = options.FactoryName
            });
        }

        var factories = new Dictionary<int, Factory>();
        for (var index = 0; index < sourceFactories.Count; index++)
        {
            var sourceFactory = sourceFactories[index];
            var code = index == 0
                ? options.FactoryCode
                : $"{options.FactoryCode}-F{sourceFactory.Id}";
            code = TruncateRequired(code.Trim(), 30);

            var factory = await db.Factories
                .IgnoreQueryFilters()
                .Where(x => x.CustomerId == customerId && x.Code == code)
                .OrderBy(x => x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (factory is null)
            {
                factory = new Factory
                {
                    CustomerId = customerId,
                    Code = code,
                    Name = TruncateRequired(
                        FirstValue(sourceFactory.Name, options.FactoryName)!,
                        150),
                    City = Truncate(sourceFactory.Location, 80),
                    CountryCode = "EGY",
                    TimeZone = "Africa/Cairo",
                    IsActive = true
                };
                db.Factories.Add(factory);
            }
            else
            {
                Restore(factory);
                factory.Name = TruncateRequired(
                    FirstValue(sourceFactory.Name, options.FactoryName)!,
                    150);
                factory.City = Truncate(sourceFactory.Location, 80);
                factory.IsActive = true;
            }

            factories[sourceFactory.Id] = factory;
        }

        return factories;
    }

    private async Task SyncDefaultFactoryShiftsAsync(
        IEnumerable<Factory> factories,
        CancellationToken cancellationToken)
    {
        var factoryIds = factories.Select(x => x.Id).Distinct().ToArray();
        var existing = await db.Shifts
            .IgnoreQueryFilters()
            .Where(x => factoryIds.Contains(x.FactoryId))
            .ToListAsync(cancellationToken);

        var definitions = new[]
        {
            new { Name = "Day Shift", Start = TimeSpan.FromHours(8), End = TimeSpan.FromHours(20) },
            new { Name = "Night Shift", Start = TimeSpan.FromHours(20), End = TimeSpan.FromHours(8) }
        };

        foreach (var factoryId in factoryIds)
        {
            foreach (var definition in definitions)
            {
                var shift = existing.FirstOrDefault(x =>
                    x.FactoryId == factoryId &&
                    (string.Equals(x.Name, definition.Name, StringComparison.OrdinalIgnoreCase) ||
                     x.StartTime == definition.Start && x.EndTime == definition.End));

                if (shift is null)
                {
                    shift = new Shift
                    {
                        FactoryId = factoryId,
                        Name = definition.Name,
                        StartTime = definition.Start,
                        EndTime = definition.End,
                        IsActive = true
                    };
                    db.Shifts.Add(shift);
                    existing.Add(shift);
                }
                else
                {
                    Restore(shift);
                    shift.StartTime = definition.Start;
                    shift.EndTime = definition.End;
                    shift.IsActive = true;
                }
            }
        }
    }

    private async Task<Dictionary<int, ProductionLine>> SyncLinesAsync(
        IReadOnlyDictionary<int, Factory> factories,
        EipicoDefinitionSnapshot snapshot,
        EipicoDefinitionSyncResult result,
        CancellationToken cancellationToken)
    {
        var lines = new Dictionary<int, ProductionLine>();
        var linesByCode = new Dictionary<string, ProductionLine>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceLine in snapshot.Lines)
        {
            if (!TryResolveFactory(factories, sourceLine.FactoryId, out var factory))
            {
                logger.LogWarning(
                    "Skipped Eipico line {LineId}: source factory {FactoryId} was not found.",
                    sourceLine.Id,
                    sourceLine.FactoryId);
                continue;
            }

            // Keep the Eipico definition name as the line code, exactly as it
            // appears in Definitions.Lines (for example E1_Cep_L2). Number is
            // only a sequence inside a factory and is not unique.
            var sourceLineName = FirstValue(sourceLine.Name, $"Line_{sourceLine.Id}")!;
            var code = TruncateRequired(sourceLineName, 20);
            var lineKey = $"{factory.Id}:{code}";

            if (!linesByCode.TryGetValue(lineKey, out var line))
            {
                line = await db.ProductionLines
                    .IgnoreQueryFilters()
                    .Where(x => x.FactoryId == factory.Id && x.Code == code)
                    .OrderBy(x => x.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken);
                if (line is null)
                {
                    line = new ProductionLine
                    {
                        FactoryId = factory.Id,
                        Code = code,
                        Name = TruncateRequired(sourceLineName, 100),
                        Number = sourceLine.Number,
                        Area = Truncate(sourceLine.Area, 100),
                        ViewName = Truncate(sourceLine.ViewName, 100),
                        AliasName = Truncate(sourceLine.AliasName, 200),
                        IsActive = true
                    };
                    db.ProductionLines.Add(line);
                }

                linesByCode.Add(lineKey, line);
            }

            Restore(line);
            line.IsActive = true;
            line.Name = TruncateRequired(sourceLineName, 100);
            line.Number = sourceLine.Number;
            line.ProductType = Truncate(sourceLine.Type, 60);
            line.Area = Truncate(sourceLine.Area, 100);
            line.ViewName = Truncate(sourceLine.ViewName, 100);
            line.AliasName = Truncate(sourceLine.AliasName, 200);
            lines[sourceLine.Id] = line;
            result.Lines++;
        }

        return lines;
    }

    private async Task SyncMachinesAsync(
        IReadOnlyDictionary<int, ProductionLine> lines,
        EipicoDefinitionSnapshot snapshot,
        EipicoDefinitionSyncResult result,
        CancellationToken cancellationToken)
    {
        foreach (var sourceMachine in snapshot.Machines)
        {
            if (!sourceMachine.LineId.HasValue ||
                !lines.TryGetValue(sourceMachine.LineId.Value, out var line))
            {
                logger.LogWarning(
                    "Skipped Eipico machine {MachineId}: its source line was not found.",
                    sourceMachine.Id);
                continue;
            }

            var code = TruncateRequired($"EIP-M{sourceMachine.Id}", 20);
            var machine = await db.Machines
                .IgnoreQueryFilters()
                .Where(x => x.LineId == line.Id && x.Code == code)
                .OrderBy(x => x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            var identity = FirstValue(sourceMachine.Name, sourceMachine.Uid, code)!;
            if (machine is null)
            {
                machine = new Machine
                {
                    LineId = line.Id,
                    Code = code,
                    Name = TruncateRequired(identity, 100),
                    MachineType = TruncateRequired(
                        FirstValue(sourceMachine.Functionality, "Eipico")!,
                        60),
                    IsActive = true
                };
                db.Machines.Add(machine);
            }
            else
            {
                Restore(machine);
                machine.Name = TruncateRequired(identity, 100);
                machine.IsActive = true;
            }

            machine.MachineType = TruncateRequired(
                FirstValue(sourceMachine.Functionality, "Eipico")!,
                60);
            machine.RatedSpeedRpm = ToNullableInt(sourceMachine.RatedSpeed);
            result.Machines++;
        }
    }

    private async Task SyncMaterialsAsync(
        Customer customer,
        EipicoDefinitionSnapshot snapshot,
        EipicoDefinitionSyncResult result,
        CancellationToken cancellationToken)
    {
        // Database queries do not return entities added earlier in this snapshot.
        // Index both existing and newly added materials by the target unique key.
        var existingMaterials = await db.Materials
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == customer.Id)
            .ToListAsync(cancellationToken);
        var materialsByCode = existingMaterials
            .GroupBy(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderBy(m => m.IsDeleted).First(),
                StringComparer.OrdinalIgnoreCase);
        var sourceCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicateCount = 0;

        foreach (var sourceMaterial in snapshot.Materials)
        {
            var code = FirstValue(sourceMaterial.ItemCode, $"EIP-MAT-{sourceMaterial.Id}")!;
            if (code.Length > 40)
                throw new InvalidDataException($"Eipico material {sourceMaterial.Id} code exceeds 40 characters.");
            if (!sourceCodes.Add(code))
                duplicateCount++;
            if (!materialsByCode.TryGetValue(code, out var material))
            {
                material = new Material
                {
                    CustomerId = customer.Id,
                    Code = code,
                    MaterialType = "RM",
                    Uom = options.DefaultMaterialUom,
                    IsActive = true
                };
                db.Materials.Add(material);
                materialsByCode.Add(code, material);
            }
            else
            {
                Restore(material);
                material.IsActive = true;
            }

            material.Name = TruncateRequired(
                FirstValue(sourceMaterial.Name, code)!,
                200);
            material.ErpCode = Truncate(sourceMaterial.ItemCode, 60);
            material.Uom = options.DefaultMaterialUom;
            result.Materials++;
        }

        if (duplicateCount > 0)
            logger.LogWarning(
                "Eipico material definitions contain {DuplicateCount} repeated ItemCodes; " +
                "they reuse the material for the same customer/code ({UniqueCount} distinct codes).",
                duplicateCount, sourceCodes.Count);
    }

    private async Task SyncSkusAsync(
        Customer customer,
        EipicoDefinitionSnapshot snapshot,
        EipicoDefinitionSyncResult result,
        CancellationToken cancellationToken)
    {
        var existingSkus = await db.Skus
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == customer.Id)
            .ToListAsync(cancellationToken);
        var skusByCode = existingSkus
            .GroupBy(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderBy(s => s.IsDeleted).First(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var sourceSku in snapshot.Skus)
        {
            var code = FirstValue(
                sourceSku.SkuCode,
                sourceSku.TechnicalSku?.ToString(),
                $"EIP-SKU-{sourceSku.Id}")!;
            if (code.Length > 40)
                throw new InvalidDataException($"Eipico SKU {sourceSku.Id} code exceeds 40 characters.");
            if (!skusByCode.TryGetValue(code, out var sku))
            {
                sku = new Sku
                {
                    CustomerId = customer.Id,
                    Code = code,
                    Uom = TruncateRequired(
                        FirstValue(sourceSku.Unit, options.DefaultSkuUom)!,
                        20),
                    StandardBatchSize = 0,
                    ErpSource = true,
                    IsActive = true
                };
                db.Skus.Add(sku);
                skusByCode.Add(code, sku);
            }
            else
            {
                Restore(sku);
                sku.IsActive = true;
            }

            sku.Name = TruncateRequired(FirstValue(sourceSku.Name, code)!, 200);
            sku.Uom = TruncateRequired(
                FirstValue(sourceSku.Unit, options.DefaultSkuUom)!,
                20);
            sku.UnitWeightG = sourceSku.Size;
            sku.ErpMaterialCode = Truncate(sourceSku.SkuCode, 60);
            sku.LastErpSyncAt = DateTime.UtcNow;
            result.Skus++;
        }
    }

    private async Task SyncScalesAsync(
        IReadOnlyDictionary<int, Factory> factories,
        EipicoDefinitionSnapshot snapshot,
        EipicoDefinitionSyncResult result,
        CancellationToken cancellationToken)
    {
        foreach (var sourceScale in snapshot.Scales)
        {
            if (!TryResolveFactory(factories, sourceScale.FactoryId, out var factory))
            {
                logger.LogWarning(
                    "Skipped Eipico scale {ScaleId}: source factory {FactoryId} was not found.",
                    sourceScale.Id,
                    sourceScale.FactoryId);
                continue;
            }

            var code = TruncateRequired($"EIP-S{sourceScale.Id}", 20);
            var scale = await db.Scales
                .IgnoreQueryFilters()
                .Where(x => x.FactoryId == factory.Id && x.Code == code)
                .OrderBy(x => x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (scale is null)
            {
                scale = new Scale
                {
                    FactoryId = factory.Id,
                    Code = code,
                    Name = TruncateRequired(
                        FirstValue(sourceScale.Name, code)!,
                        100),
                    Zone = Truncate(sourceScale.Room, 20),
                    Protocol = Truncate(sourceScale.ScaleTypeDescription, 30),
                    TareKg = 0,
                    ScaleState = "offline"
                };
                db.Scales.Add(scale);
            }
            else
            {
                Restore(scale);
                scale.Name = TruncateRequired(
                    FirstValue(sourceScale.Name, code)!,
                    100);
                scale.Zone = Truncate(sourceScale.Room, 20);
                scale.Protocol = Truncate(sourceScale.ScaleTypeDescription, 30);
            }

            result.Scales++;
        }
    }

    private async Task<Dictionary<int, Transformer>> SyncTransformersAsync(
        IReadOnlyDictionary<int, Factory> factories,
        EipicoDefinitionSnapshot snapshot,
        EipicoDefinitionSyncResult result,
        CancellationToken cancellationToken)
    {
        var transformers = new Dictionary<int, Transformer>();
        foreach (var sourceTransformer in snapshot.Transformers)
        {
            var name = FirstValue(
                sourceTransformer.SourceId,
                sourceTransformer.Name,
                $"EIP-TR-{sourceTransformer.Id}")!;
            if (!TryResolveFactory(factories, sourceTransformer.FactoryId, out var factory))
            {
                logger.LogWarning(
                    "Skipped Eipico transformer {TransformerId}: source factory {FactoryId} was not found.",
                    sourceTransformer.Id,
                    sourceTransformer.FactoryId);
                continue;
            }

            var transformer = await db.Transformers
                .IgnoreQueryFilters()
                .Where(x => x.TransformerName == name)
                .FirstOrDefaultAsync(cancellationToken);
            if (transformer is null)
            {
                transformer = new Transformer
                {
                    TransformerName = TruncateRequired(name, 150),
                    FactoryId = factory.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                db.Transformers.Add(transformer);
            }
            else
            {
                transformer.FactoryId = factory.Id;
                transformer.IsDeleted = false;
            }

            transformers[sourceTransformer.Id] = transformer;
            result.Transformers++;
        }

        return transformers;
    }

    private async Task SyncLineTransformersAsync(
        IReadOnlyDictionary<int, ProductionLine> lines,
        IReadOnlyDictionary<int, Transformer> transformers,
        EipicoDefinitionSnapshot snapshot,
        EipicoDefinitionSyncResult result,
        CancellationToken cancellationToken)
    {
        foreach (var sourceLink in snapshot.LineTransformers)
        {
            if (!lines.TryGetValue(sourceLink.LineId, out var line))
            {
                logger.LogWarning(
                    "Skipped Eipico line-transformer link: source line {LineId} was not synchronized.",
                    sourceLink.LineId);
                continue;
            }

            if (!transformers.TryGetValue(sourceLink.TransformerId, out var transformer))
            {
                logger.LogWarning(
                    "Skipped Eipico line-transformer link: source transformer {TransformerId} was not synchronized.",
                    sourceLink.TransformerId);
                continue;
            }

            var exists = await db.ProductionLineTransformers.AnyAsync(
                x => x.ProductionLineId == line.Id && x.TransformerId == transformer.Id,
                cancellationToken);
            if (!exists)
            {
                db.ProductionLineTransformers.Add(new ProductionLineTransformer
                {
                    ProductionLineId = line.Id,
                    TransformerId = transformer.Id
                });
            }

            result.LineTransformers++;
        }
    }

    private static bool TryResolveFactory(
        IReadOnlyDictionary<int, Factory> factories,
        int? sourceFactoryId,
        out Factory factory)
    {
        if (sourceFactoryId.HasValue &&
            factories.TryGetValue(sourceFactoryId.Value, out factory!))
        {
            return true;
        }

        // A null source factory is only unambiguous when the source snapshot
        // contains one factory. With multiple factories, silently assigning the
        // row to the first one would recreate the original data corruption.
        if (factories.Count == 1)
        {
            factory = factories.Values.Single();
            return true;
        }

        factory = null!;
        return false;
    }

    private async Task SyncTagsAsync(
        EipicoDefinitionSnapshot snapshot,
        EipicoDefinitionSyncResult result,
        CancellationToken cancellationToken)
    {
        var deviceType = await db.DeviceTypes
            .IgnoreQueryFilters()
            .Where(x => x.Code == "EIPICO_MACHINE")
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (deviceType is null)
        {
            deviceType = new DeviceType
            {
                Code = "EIPICO_MACHINE",
                Label = "Eipico machine signal",
                Protocol = "SQL",
                IsActive = true
            };
            db.DeviceTypes.Add(deviceType);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            Restore(deviceType);
            deviceType.IsActive = true;
        }

        foreach (var sourceTag in snapshot.Tags)
        {
            var tagName = FirstValue(
                sourceTag.ColumnName,
                sourceTag.DisplayName,
                $"Tag_{sourceTag.Id}")!;
            if (tagName.Length > 100)
                throw new InvalidDataException($"Eipico tag {sourceTag.Id} name exceeds 100 characters.");
            var tag = db.DeviceTypeTagDefinitions.Local.FirstOrDefault(x =>
                x.DeviceTypeId == deviceType.Id &&
                string.Equals(x.TagName.Trim(), tagName, StringComparison.OrdinalIgnoreCase))
                ?? await db.DeviceTypeTagDefinitions
                .IgnoreQueryFilters()
                .Where(x => x.DeviceTypeId == deviceType.Id && x.TagName == tagName)
                .OrderBy(x => x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            if (tag is null)
            {
                tag = new DeviceTypeTagDefinition
                {
                    DeviceTypeId = deviceType.Id,
                    TagName = tagName,
                    DataType = "number",
                    IsRequired = false
                };
                db.DeviceTypeTagDefinitions.Add(tag);
            }
            else
            {
                Restore(tag);
            }

            result.Tags++;
        }
    }

    private static string? FirstValue(params string?[] values)
        => values.Select(x => x?.Trim())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static string Required(string value, int maxLength)
        => TruncateRequired(value.Trim(), maxLength);

    private static string TruncateRequired(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : TruncateRequired(value.Trim(), maxLength);

    private static int? ToNullableInt(decimal? value)
    {
        if (!value.HasValue || value < 0 || value > int.MaxValue)
            return null;
        return decimal.ToInt32(decimal.Round(value.Value, 0, MidpointRounding.AwayFromZero));
    }

    private static void Restore(Domain.Common.SoftDeleteEntity entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }
}

internal sealed class EipicoDefinitionSyncResult
{
    public int Factories { get; set; }
    public int Lines { get; set; }
    public int Machines { get; set; }
    public int Materials { get; set; }
    public int Skus { get; set; }
    public int Scales { get; set; }
    public int Transformers { get; set; }
    public int LineTransformers { get; set; }
    public int Tags { get; set; }
}
