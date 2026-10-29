using Domain.Entities.Integration;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Domain.Entities.Production;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Integration;

internal sealed class EipicoSapSyncService(
    MesDbContext db,
    EipicoSourceReader source,
    EipicoSourceTime sourceTime,
    IOptions<EipicoSyncOptions> configuredOptions,
    ILogger<EipicoSapSyncService> logger)
{
    private const string SourceSystem = "Eipico";
    private const string StreamName = "SAPOrders";
    private const string LiveStartStream = "SAPOrders.LiveStart";
    private const string SourceType = "sap";
    private readonly EipicoSyncOptions options = configuredOptions.Value;
    private Customer? cachedExistingCustomer;
    private Factory? cachedExistingFactory;
    private readonly Dictionary<string, ProductionLine> cachedExistingLines =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Sku> cachedExistingSkus =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<int> SyncNextPageAsync(CancellationToken cancellationToken)
    {
        await EnsureLiveStartAsync(cancellationToken);
        await RetryErrorsAsync(cancellationToken);

        var lastSourceId = await db.SyncCheckpoints
            .AsNoTracking()
            .Where(x => x.SourceSystem == SourceSystem && x.StreamName == StreamName)
            .Select(x => (long?)x.LastSourceId)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;

        var orders = await source.ReadOrdersAfterAsync(
            lastSourceId,
            options.BatchSize,
            cancellationToken);

        foreach (var order in orders)
        {
            try
            {
                await ApplyOrderAsync(order, advanceCheckpoint: true, cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                await ResetDatabaseAfterFailureAsync();
                await RecordFailureAsync(
                    order.Id,
                    exception,
                    advanceCheckpoint: true,
                    terminal: true,
                    cancellationToken);

                logger.LogWarning(
                    "Skipped invalid Eipico SAP order {SourceRecordId}: {Reason}",
                    order.Id,
                    exception.Message);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await ResetDatabaseAfterFailureAsync();
                await RecordFailureAsync(
                    order.Id,
                    exception,
                    advanceCheckpoint: true,
                    terminal: false,
                    cancellationToken);

                logger.LogError(
                    exception,
                    "Could not synchronize Eipico SAP order {SourceRecordId}; it was placed in the retry queue.",
                    order.Id);
            }
        }

        return orders.Count;
    }

    public async Task<int> ReconcileRecentOrdersAsync(CancellationToken cancellationToken)
    {
        var liveStart = await EnsureLiveStartAsync(cancellationToken);
        var orders = await source.ReadRecentOrdersAsync(
            options.ReconciliationBatchSize,
            cancellationToken);

        if (orders.Count == 0)
            return 0;

        // Reconcile old orders only when they were actually imported as a
        // dependency. A recent-page scan must not reintroduce historical rows.
        var ids = orders.Select(x => (long)x.Id).ToArray();
        var importedIds = await db.Batches.AsNoTracking()
            .Where(x => x.SourceType == SourceType && x.SourceRecordId.HasValue && ids.Contains(x.SourceRecordId.Value))
            .Select(x => x.SourceRecordId!.Value)
            .ToHashSetAsync(cancellationToken);
        orders = orders.Where(x => x.Id > liveStart || importedIds.Contains(x.Id)).ToArray();

        var reconciledCount = 0;
        foreach (var order in orders)
        {
            try
            {
                await ApplyOrderAsync(order, advanceCheckpoint: false, cancellationToken);
                reconciledCount++;
            }
            catch (InvalidDataException exception)
            {
                await ResetDatabaseAfterFailureAsync();
                await RecordFailureAsync(
                    order.Id,
                    exception,
                    advanceCheckpoint: false,
                    terminal: true,
                    cancellationToken);

                logger.LogWarning(
                    "Could not reconcile invalid Eipico SAP order {SourceRecordId}: {Reason}",
                    order.Id,
                    exception.Message);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await ResetDatabaseAfterFailureAsync();
                await RecordFailureAsync(
                    order.Id,
                    exception,
                    advanceCheckpoint: false,
                    terminal: false,
                    cancellationToken);

                logger.LogWarning(
                    exception,
                    "Could not reconcile Eipico SAP order {SourceRecordId}.",
                    order.Id);
            }
        }

        return reconciledCount;
    }

    // Called inside a realtime record's transaction: import only the SAP order
    // required by that record, without moving the live SAP checkpoint.
    public async Task ImportDependencyAsync(
        int? orderId, int? materialId, string? batchUid, string? materialUid,
        CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("SAP dependencies require the caller's realtime transaction.");
        var order = await source.ReadDependencyOrderAsync(
            orderId, materialId, batchUid, materialUid, cancellationToken)
            ?? throw new EipicoDependencyNotReadyException("No source SAP order matches the realtime dependency.");
        await UpsertOrderAsync(order, cancellationToken);
        await ResolveErrorsAsync(order.Id, cancellationToken);
    }

    private async Task<long> EnsureLiveStartAsync(CancellationToken cancellationToken)
    {
        if (!options.InitializeMissingSapCheckpoints)
            return -1;
        var start = await db.SyncCheckpoints.AsNoTracking()
            .Where(x => x.SourceSystem == SourceSystem && x.StreamName == LiveStartStream)
            .Select(x => (long?)x.LastSourceId)
            .SingleOrDefaultAsync(cancellationToken);
        if (start.HasValue)
            return start.Value;

        var sourceMax = await source.GetMaxOrderIdAsync(cancellationToken);
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // A transient commit failure may retry after the first attempt
            // committed. The persisted marker makes that retry idempotent.
            if (await db.SyncCheckpoints.AnyAsync(x =>
                x.SourceSystem == SourceSystem && x.StreamName == LiveStartStream, cancellationToken))
                return;
            var now = DateTime.UtcNow;
            db.SyncCheckpoints.Add(new SyncCheckpoint
            {
                SourceSystem = SourceSystem, StreamName = LiveStartStream,
                LastSourceId = sourceMax, UpdatedAt = now
            });
            await AdvanceCheckpointAsync(sourceMax, cancellationToken);
            var historicalErrors = await db.SyncErrors.Where(x =>
                x.SourceSystem == SourceSystem && x.StreamName == StreamName &&
                x.SourceRecordId <= sourceMax && x.ResolvedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var error in historicalErrors)
            {
                error.ErrorMessage = "SkippedHistoricalSapOrder: excluded from bulk import at live cutover. Previous: " + error.ErrorMessage;
                error.ResolvedAt = now;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
        });
        logger.LogInformation(
            "Initialized live SAP import at source id {SourceId}. Older orders are imported only when required by a realtime record.",
            sourceMax);
        return sourceMax;
    }

    private async Task RetryErrorsAsync(CancellationToken cancellationToken)
    {
        var retryBefore = DateTime.UtcNow.AddSeconds(-Math.Max(1, options.ErrorRetryIntervalSeconds));
        var failedSourceIds = await db.SyncErrors
            .AsNoTracking()
            .Where(x =>
                x.SourceSystem == SourceSystem &&
                x.StreamName == StreamName &&
                x.SourceRecordId.HasValue &&
                x.ResolvedAt == null &&
                x.LastOccurredAt <= retryBefore)
            .Select(x => x.SourceRecordId!.Value)
            .Distinct()
            .OrderBy(x => x)
            .Take(Math.Clamp(options.RetryBatchSize, 1, 100))
            .ToArrayAsync(cancellationToken);

        foreach (var sourceId in failedSourceIds)
        {
            var order = await source.ReadOrderByIdAsync(sourceId, cancellationToken);
            if (order is null)
            {
                await ResolveErrorsAsync(sourceId, cancellationToken);
                continue;
            }

            try
            {
                await ApplyOrderAsync(order, advanceCheckpoint: false, cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                await ResetDatabaseAfterFailureAsync();
                await RecordFailureAsync(
                    sourceId,
                    exception,
                    advanceCheckpoint: false,
                    terminal: true,
                    cancellationToken);

                logger.LogWarning(
                    "Marked invalid Eipico SAP order {SourceRecordId} as skipped: {Reason}",
                    sourceId,
                    exception.Message);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await ResetDatabaseAfterFailureAsync();
                await RecordFailureAsync(
                    sourceId,
                    exception,
                    advanceCheckpoint: false,
                    terminal: false,
                    cancellationToken);

                logger.LogWarning(
                    exception,
                    "Retry failed for Eipico SAP order {SourceRecordId}.",
                    sourceId);
            }
        }
    }

    private async Task ApplyOrderAsync(
        EipicoSapOrderRecord order,
        bool advanceCheckpoint,
        CancellationToken cancellationToken)
    {
        var executionStrategy = db.Database.CreateExecutionStrategy();
        try
        {
            await executionStrategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    await UpsertOrderAsync(order, cancellationToken);
                    await ResolveErrorsAsync(order.Id, cancellationToken);
                    if (advanceCheckpoint)
                        await AdvanceCheckpointAsync(order.Id, cancellationToken);

                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                    catch (Exception rollbackException)
                    {
                        logger.LogWarning(
                            rollbackException,
                            "Could not roll back the failed Eipico SAP synchronization transaction. The connection will be reset.");
                    }

                    logger.LogDebug(exception, "Rolling back Eipico SAP order {SourceRecordId}.", order.Id);
                    throw;
                }
            });
        }
        catch
        {
            await ResetDatabaseAfterFailureAsync();
            throw;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task ResetDatabaseAfterFailureAsync()
    {
        db.ChangeTracker.Clear();

        if (db.Database.CurrentTransaction is { } transaction)
        {
            try
            {
                await transaction.DisposeAsync();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not dispose the failed Eipico SAP synchronization transaction.");
            }
        }

        try
        {
            await db.Database.CloseConnectionAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not close the failed Eipico SAP synchronization database connection.");
        }

        db.ChangeTracker.Clear();
    }

    private async Task UpsertOrderAsync(
        EipicoSapOrderRecord sourceOrder,
        CancellationToken cancellationToken)
    {
        var batchUid = Required(sourceOrder.BatchId, "BatchId", 30);
        var sourceLine = await source.ResolveOrderLineAsync(batchUid, cancellationToken)
            ?? throw new EipicoDependencyNotReadyException(
                $"SAP batch '{batchUid}' has no unique line in Eipico MachineLoads yet.");
        var lineCode = Required(sourceLine.LineName, "DefinitionLineName", 20);
        var skuCode = Required(sourceOrder.SkuCode, "SKUCode", 40);
        var skuName = Required(sourceOrder.SkuName, "SKUName", 200);
        var jobOrderId = Optional(sourceOrder.JobOrderId, "JobOrderId", 100);
        var warehouseCode = Optional(sourceOrder.WarehouseCode, "WarehouseCode", 50);
        var storageLocation = Optional(sourceOrder.StorageLocation, "StorageLocation", 50);

        if (sourceOrder.Materials.Count == 0)
            throw new InvalidDataException($"SAP order {sourceOrder.Id} has no active materials.");
        if (sourceOrder.Materials.Select(x => x.Id).Distinct().Count() != sourceOrder.Materials.Count)
            throw new InvalidDataException($"SAP order {sourceOrder.Id} contains duplicate material IDs.");

        var customer = await EnsureCustomerAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var line = await db.ProductionLines
            .IgnoreQueryFilters()
            .Include(x => x.Factory)
            .SingleOrDefaultAsync(x =>
                !x.IsDeleted && x.Code == lineCode &&
                x.Factory.CustomerId == customer.Id &&
                x.Factory.Name == sourceLine.FactoryName,
                cancellationToken)
            ?? throw new EipicoDependencyNotReadyException(
                $"SAP batch '{batchUid}' line '{lineCode}' in factory '{sourceLine.FactoryName}' is not synchronized yet.");
        var sku = await EnsureSkuAsync(customer.Id, skuCode, skuName, cancellationToken);

        var materialInputs = new List<(
            EipicoSapMaterialRecord Source,
            string ItemCode,
            string Name,
            string Unit)>();
        foreach (var sourceMaterial in sourceOrder.Materials)
        {
            var itemCode = Required(sourceMaterial.ItemCode, "Material.ItemCode", 40);
            var materialName = Required(sourceMaterial.Name, "Material.Name", 200);
            var unit = MaterialUnit(sourceMaterial.Unit);
            Required(sourceMaterial.Uid, "Material.UID", 30);
            Optional(sourceMaterial.OperationCode, "Material.OpCode", 100);

            if (sourceMaterial.Quantity < 0)
                throw new InvalidDataException(
                    $"SAP material {sourceMaterial.Id} has a negative quantity.");

            materialInputs.Add((sourceMaterial, itemCode, materialName, unit));
        }

        var itemCodes = materialInputs.Select(x => x.ItemCode).Distinct().ToArray();
        var existingMaterials = await db.Materials
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == customer.Id && itemCodes.Contains(x.Code))
            .OrderBy(x => x.IsDeleted)
            .ToListAsync(cancellationToken);
        var materialsByCode = existingMaterials
            .GroupBy(x => x.Code)
            .ToDictionary(x => x.Key, x => x.First());
        var preparedMaterials = new List<(EipicoSapMaterialRecord Source, Material Target)>();

        foreach (var input in materialInputs)
        {
            if (!materialsByCode.TryGetValue(input.ItemCode, out var targetMaterial))
            {
                targetMaterial = new Material
                {
                    CustomerId = customer.Id,
                    Code = input.ItemCode,
                    MaterialType = "RM",
                    IsActive = true
                };
                db.Materials.Add(targetMaterial);
                materialsByCode.Add(input.ItemCode, targetMaterial);
            }
            else
            {
                Restore(targetMaterial);
            }

            targetMaterial.Name = input.Name;
            targetMaterial.Uom = input.Unit;
            targetMaterial.ErpCode = input.ItemCode;
            targetMaterial.IsActive = true;
            preparedMaterials.Add((input.Source, targetMaterial));
        }

        await db.SaveChangesAsync(cancellationToken);

        var sameUidBatch = await db.Batches
            .IgnoreQueryFilters()
            .Where(x => x.Uid == batchUid)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (sameUidBatch is not null &&
            !string.Equals(sameUidBatch.SourceType, SourceType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Batch UID '{batchUid}' already belongs to source type '{sameUidBatch.SourceType}'.");
        }

        var batch = sameUidBatch;
        if (batch is null)
        {
            batch = new Batch
            {
                Uid = batchUid,
                SourceType = SourceType,
                Status = "planned",
                Stage = "warehouse"
            };
            db.Batches.Add(batch);
        }
        else
        {
            Restore(batch);
        }

        batch.SourceRecordId = sourceOrder.Id;
        batch.JobOrderId = jobOrderId;
        batch.WarehouseCode = warehouseCode;
        batch.StorageLocation = storageLocation;
        batch.ReceivedAt = sourceTime.ToUtc(sourceOrder.ReceivedAt);
        batch.CustomerId = customer.Id;
        batch.SkuId = sku.Id;
        batch.LineId = line.Id;

        await db.SaveChangesAsync(cancellationToken);

        var targetBatchMaterials = await db.BatchMaterials
            .IgnoreQueryFilters()
            .Where(x => x.BatchId == batch.Id)
            .ToListAsync(cancellationToken);
        var currentSourceIds = sourceOrder.Materials.Select(x => (long?)x.Id).ToHashSet();

        foreach (var (sourceMaterial, targetMaterial) in preparedMaterials)
        {
            var materialUid = Required(sourceMaterial.Uid, "Material.UID", 30);
            var itemCode = Required(sourceMaterial.ItemCode, "Material.ItemCode", 40);
            var unit = MaterialUnit(sourceMaterial.Unit);
            var operationCode = Optional(sourceMaterial.OperationCode, "Material.OpCode", 100);

            var batchMaterial = targetBatchMaterials
                .SingleOrDefault(x => x.SourceRecordId == sourceMaterial.Id);
            if (batchMaterial is null)
            {
                batchMaterial = new BatchMaterial
                {
                    BatchId = batch.Id,
                    SourceRecordId = sourceMaterial.Id,
                    DispensedQty = 0,
                    Status = "pending"
                };
                db.BatchMaterials.Add(batchMaterial);
                targetBatchMaterials.Add(batchMaterial);
            }
            else
            {
                Restore(batchMaterial);
            }

            batchMaterial.MaterialId = targetMaterial.Id;
            batchMaterial.MaterialUid = materialUid;
            batchMaterial.ItemCode = itemCode;
            batchMaterial.OperationCode = operationCode;
            batchMaterial.Uom = unit;
            batchMaterial.PlannedQty = sourceMaterial.Quantity;
        }

        var utcNow = DateTime.UtcNow;
        foreach (var removedMaterial in targetBatchMaterials.Where(x =>
                     !x.IsDeleted &&
                     x.SourceRecordId.HasValue &&
                     !currentSourceIds.Contains(x.SourceRecordId)))
        {
            removedMaterial.IsDeleted = true;
            removedMaterial.DeletedAt = utcNow;
            removedMaterial.UpdatedAt = utcNow;
        }
    }

    private async Task<Customer> EnsureCustomerAsync(CancellationToken cancellationToken)
    {
        if (cachedExistingCustomer is not null)
            return cachedExistingCustomer;

        var code = Required(options.CustomerCode, "EipicoSync.CustomerCode", 30);
        var name = Required(options.CustomerName, "EipicoSync.CustomerName", 150);
        var customer = await db.Customers
            .IgnoreQueryFilters()
            .Where(x => x.Code == code)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Code = code,
                Name = name,
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
            customer.Name = name;
            customer.IsActive = true;
            cachedExistingCustomer = customer;
        }

        return customer;
    }

    private async Task<Factory> EnsureFactoryAsync(int customerId, CancellationToken cancellationToken)
    {
        if (cachedExistingFactory is not null && cachedExistingFactory.CustomerId == customerId)
            return cachedExistingFactory;

        var code = Required(options.FactoryCode, "EipicoSync.FactoryCode", 30);
        var name = Required(options.FactoryName, "EipicoSync.FactoryName", 150);
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
                Name = name,
                CountryCode = "EGY",
                TimeZone = "Africa/Cairo",
                IsActive = true
            };
            db.Factories.Add(factory);
        }
        else
        {
            Restore(factory);
            factory.Name = name;
            factory.IsActive = true;
            cachedExistingFactory = factory;
        }

        return factory;
    }

    private async Task<ProductionLine> EnsureLineAsync(
        int factoryId,
        string code,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{factoryId}:{code}";
        if (cachedExistingLines.TryGetValue(cacheKey, out var cachedLine))
            return cachedLine;

        var line = await db.ProductionLines
            .IgnoreQueryFilters()
            .Where(x => x.FactoryId == factoryId && x.Code == code)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (line is null)
        {
            line = new ProductionLine
            {
                FactoryId = factoryId,
                Code = code,
                Name = code,
                IsActive = true
            };
            db.ProductionLines.Add(line);
        }
        else
        {
            Restore(line);
            line.IsActive = true;
            cachedExistingLines[cacheKey] = line;
        }

        return line;
    }

    private async Task<Sku> EnsureSkuAsync(
        int customerId,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{customerId}:{code}";
        if (cachedExistingSkus.TryGetValue(cacheKey, out var cachedSku))
            return cachedSku;

        var sku = await db.Skus
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == customerId && x.Code == code)
            .OrderBy(x => x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (sku is null)
        {
            sku = new Sku
            {
                CustomerId = customerId,
                Code = code,
                Uom = Required(options.DefaultSkuUom, "EipicoSync.DefaultSkuUom", 20),
                StandardBatchSize = 0,
                ErpSource = true,
                IsActive = true
            };
            db.Skus.Add(sku);
        }
        else
        {
            Restore(sku);
            cachedExistingSkus[cacheKey] = sku;
        }

        sku.Name = name;
        sku.ErpMaterialCode = code;
        sku.LastErpSyncAt = DateTime.UtcNow;
        sku.ErpSource = true;
        sku.IsActive = true;
        return sku;
    }

    private async Task AdvanceCheckpointAsync(long sourceId, CancellationToken cancellationToken)
    {
        var checkpoint = await db.SyncCheckpoints.SingleOrDefaultAsync(
            x => x.SourceSystem == SourceSystem && x.StreamName == StreamName,
            cancellationToken);

        if (checkpoint is null)
        {
            checkpoint = new SyncCheckpoint
            {
                SourceSystem = SourceSystem,
                StreamName = StreamName,
                LastSourceId = sourceId,
                UpdatedAt = DateTime.UtcNow
            };
            db.SyncCheckpoints.Add(checkpoint);
        }
        else if (sourceId > checkpoint.LastSourceId)
        {
            checkpoint.LastSourceId = sourceId;
            checkpoint.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task RecordFailureAsync(
        long sourceId,
        Exception exception,
        bool advanceCheckpoint,
        bool terminal,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var error = await db.SyncErrors
            .Where(x =>
                x.SourceSystem == SourceSystem &&
                x.StreamName == StreamName &&
                x.SourceRecordId == sourceId &&
                x.ResolvedAt == null)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (error is null)
        {
            error = new SyncError
            {
                SourceSystem = SourceSystem,
                StreamName = StreamName,
                SourceRecordId = sourceId,
                FirstOccurredAt = utcNow
            };
            db.SyncErrors.Add(error);
        }

        error.ErrorMessage = $"{exception.GetType().Name}: {exception.Message}";
        error.AttemptCount++;
        error.LastOccurredAt = utcNow;
        error.ResolvedAt = terminal ? utcNow : null;
        if (advanceCheckpoint)
            await AdvanceCheckpointAsync(sourceId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private async Task ResolveErrorsAsync(long sourceId, CancellationToken cancellationToken)
    {
        var errors = await db.SyncErrors
            .Where(x =>
                x.SourceSystem == SourceSystem &&
                x.StreamName == StreamName &&
                x.SourceRecordId == sourceId &&
                x.ResolvedAt == null)
            .ToListAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        foreach (var error in errors)
            error.ResolvedAt = utcNow;

        if (errors.Count > 0 && db.Database.CurrentTransaction is null)
            await db.SaveChangesAsync(cancellationToken);
    }

    private static string Required(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidDataException($"{fieldName} is required.");
        if (normalized.Length > maxLength)
            throw new InvalidDataException(
                $"{fieldName} exceeds the target maximum length of {maxLength} characters.");
        return normalized;
    }

    private static string? Optional(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length > maxLength)
            throw new InvalidDataException(
                $"{fieldName} exceeds the target maximum length of {maxLength} characters.");
        return normalized;
    }

    private string MaterialUnit(string? sourceUnit)
        => Optional(sourceUnit, "Material.Unit", 20)
           ?? Required(options.DefaultMaterialUom, "EipicoSync.DefaultMaterialUom", 20);

    private static void Restore(Domain.Common.SoftDeleteEntity entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }
}
