using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Integration;

/// <summary>
/// Maps EMS electrical hierarchy metadata onto the existing Genie master data.
/// Factory, line and transformer core fields are verification-only and are
/// never modified by this service. Nullable EMS ids only record provenance so
/// dashboard queries cannot mix EMS definitions with unrelated Genie rows.
/// </summary>
public sealed class EmsEnergyDefinitionSyncService(
    MesDbContext db,
    EmsEnergySourceReader source)
{
    public async Task<EmsDefinitionSyncResult> SyncAndVerifyAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await source.ReadDefinitionsAsync(cancellationToken);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var factories = await ResolveFactoriesAsync(snapshot, cancellationToken);
            var transformers = await ResolveTransformersAsync(
                snapshot,
                factories,
                cancellationToken);
            var zones = await SyncZonesAsync(
                snapshot,
                factories,
                transformers,
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            var lineCount = await MapLinesAsync(
                snapshot,
                factories,
                zones,
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            await VerifyAsync(snapshot, factories, transformers, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new EmsDefinitionSyncResult(
                factories.Count,
                transformers.Count,
                zones.Count,
                lineCount);
        });
    }

    private async Task<Dictionary<int, Factory>> ResolveFactoriesAsync(
        EmsDefinitionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var existing = await db.Factories
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var result = new Dictionary<int, Factory>();

        foreach (var sourceFactory in snapshot.Factories.Where(x => !x.IsDeleted))
        {
            var matches = existing.Where(x => string.Equals(
                    x.Name.Trim(),
                    sourceFactory.Name.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count != 1)
            {
                throw new InvalidDataException(
                    $"EMS factory {sourceFactory.Id} ('{sourceFactory.Name}') must match exactly one existing Genie factory; found {matches.Count}.");
            }

            matches[0].EmsEnergySourceId = sourceFactory.Id;
            result.Add(sourceFactory.Id, matches[0]);
        }

        return result;
    }

    private async Task<Dictionary<int, Transformer>> ResolveTransformersAsync(
        EmsDefinitionSnapshot snapshot,
        IReadOnlyDictionary<int, Factory> factories,
        CancellationToken cancellationToken)
    {
        var existing = await db.Transformers
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var result = new Dictionary<int, Transformer>();

        foreach (var sourceTransformer in snapshot.Transformers.Where(x => !x.IsDeleted))
        {
            if (!sourceTransformer.FactoryId.HasValue ||
                !factories.TryGetValue(sourceTransformer.FactoryId.Value, out var factory))
            {
                throw new InvalidDataException(
                    $"EMS transformer {sourceTransformer.Id} references an unmapped factory.");
            }

            var matches = existing.Where(x =>
                    x.FactoryId == factory.Id &&
                    string.Equals(
                        x.TransformerName.Trim(),
                        sourceTransformer.TransformerName.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count != 1)
            {
                throw new InvalidDataException(
                    $"EMS transformer {sourceTransformer.Id} ('{sourceTransformer.TransformerName}') must match exactly one existing Genie transformer; found {matches.Count}.");
            }

            matches[0].EmsEnergySourceId = sourceTransformer.Id;
            result.Add(sourceTransformer.Id, matches[0]);
        }

        return result;
    }

    private async Task<Dictionary<int, EnergyZone>> SyncZonesAsync(
        EmsDefinitionSnapshot snapshot,
        IReadOnlyDictionary<int, Factory> factories,
        IReadOnlyDictionary<int, Transformer> transformers,
        CancellationToken cancellationToken)
    {
        var existing = await db.EnergyZones.ToListAsync(cancellationToken);
        var bySourceId = existing.ToDictionary(x => x.EmsSourceId);
        var sourceIds = snapshot.Zones.Select(x => x.Id).ToHashSet();

        foreach (var oldZone in existing.Where(x => !sourceIds.Contains(x.EmsSourceId)))
            oldZone.IsDeleted = true;

        var result = new Dictionary<int, EnergyZone>();
        foreach (var sourceZone in snapshot.Zones)
        {
            if (!factories.TryGetValue(sourceZone.FactoryId, out var factory))
            {
                throw new InvalidDataException(
                    $"EMS zone {sourceZone.Id} references unmapped factory {sourceZone.FactoryId}.");
            }

            Transformer? transformer = null;
            if (sourceZone.TransformerId.HasValue &&
                !transformers.TryGetValue(sourceZone.TransformerId.Value, out transformer))
            {
                throw new InvalidDataException(
                    $"EMS zone {sourceZone.Id} references unmapped transformer {sourceZone.TransformerId}.");
            }

            if (!bySourceId.TryGetValue(sourceZone.Id, out var zone))
            {
                zone = new EnergyZone { EmsSourceId = sourceZone.Id };
                db.EnergyZones.Add(zone);
            }

            zone.FactoryId = factory.Id;
            zone.TransformerId = transformer?.Id;
            zone.Name = sourceZone.Name;
            zone.RatioFromParent = sourceZone.RatioFromParent;
            zone.SourceCreatedAt = sourceZone.CreatedAt;
            zone.IsDeleted = sourceZone.IsDeleted;
            result.Add(sourceZone.Id, zone);
        }

        return result;
    }

    private async Task<int> MapLinesAsync(
        EmsDefinitionSnapshot snapshot,
        IReadOnlyDictionary<int, Factory> factories,
        IReadOnlyDictionary<int, EnergyZone> zones,
        CancellationToken cancellationToken)
    {
        var existing = await db.ProductionLines
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var mappedCount = 0;

        foreach (var sourceLine in snapshot.Lines.Where(x => !x.IsDeleted))
        {
            if (!sourceLine.FactoryId.HasValue ||
                !factories.TryGetValue(sourceLine.FactoryId.Value, out var factory))
            {
                throw new InvalidDataException(
                    $"EMS line {sourceLine.Id} references an unmapped factory.");
            }

            var matches = existing.Where(x =>
                    x.FactoryId == factory.Id &&
                    string.Equals(
                        x.Code.Trim(),
                        sourceLine.Name.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count != 1)
            {
                throw new InvalidDataException(
                    $"EMS line {sourceLine.Id} ('{sourceLine.Name}') must match exactly one existing Genie production line; found {matches.Count}.");
            }

            EnergyZone? zone = null;
            if (sourceLine.ZoneId.HasValue &&
                !zones.TryGetValue(sourceLine.ZoneId.Value, out zone))
            {
                throw new InvalidDataException(
                    $"EMS line {sourceLine.Id} references unmapped zone {sourceLine.ZoneId}.");
            }

            matches[0].EnergyZone = zone;
            matches[0].EnergyRatioFromParent = sourceLine.RatioFromParent;
            matches[0].EmsEnergySourceId = sourceLine.Id;
            mappedCount++;
        }

        return mappedCount;
    }

    private async Task VerifyAsync(
        EmsDefinitionSnapshot snapshot,
        IReadOnlyDictionary<int, Factory> factories,
        IReadOnlyDictionary<int, Transformer> transformers,
        CancellationToken cancellationToken)
    {
        var zones = await db.EnergyZones.AsNoTracking().ToDictionaryAsync(
            x => x.EmsSourceId,
            cancellationToken);
        foreach (var sourceZone in snapshot.Zones)
        {
            if (!zones.TryGetValue(sourceZone.Id, out var targetZone) ||
                targetZone.FactoryId != factories[sourceZone.FactoryId].Id ||
                targetZone.TransformerId != ResolveTransformerId(sourceZone, transformers) ||
                targetZone.Name != sourceZone.Name ||
                targetZone.RatioFromParent != sourceZone.RatioFromParent ||
                targetZone.SourceCreatedAt != sourceZone.CreatedAt ||
                targetZone.IsDeleted != sourceZone.IsDeleted)
            {
                throw new InvalidDataException(
                    $"EMS energy-zone verification failed for source id {sourceZone.Id}.");
            }
        }

        foreach (var sourceFactory in snapshot.Factories.Where(x => !x.IsDeleted))
        {
            if (factories[sourceFactory.Id].EmsEnergySourceId != sourceFactory.Id)
            {
                throw new InvalidDataException(
                    $"EMS factory provenance verification failed for source id {sourceFactory.Id}.");
            }
        }

        foreach (var sourceTransformer in snapshot.Transformers.Where(x => !x.IsDeleted))
        {
            if (transformers[sourceTransformer.Id].EmsEnergySourceId != sourceTransformer.Id)
            {
                throw new InvalidDataException(
                    $"EMS transformer provenance verification failed for source id {sourceTransformer.Id}.");
            }
        }

        var targetLines = await db.ProductionLines
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var sourceLine in snapshot.Lines.Where(x => !x.IsDeleted))
        {
            var targetFactoryId = factories[sourceLine.FactoryId!.Value].Id;
            var targetLine = targetLines.Single(x =>
                x.FactoryId == targetFactoryId &&
                string.Equals(x.Code, sourceLine.Name, StringComparison.OrdinalIgnoreCase));
            var expectedZoneId = sourceLine.ZoneId.HasValue
                ? zones[sourceLine.ZoneId.Value].Id
                : (int?)null;
            if (targetLine.EnergyZoneId != expectedZoneId ||
                targetLine.EnergyRatioFromParent != sourceLine.RatioFromParent ||
                targetLine.EmsEnergySourceId != sourceLine.Id)
            {
                throw new InvalidDataException(
                    $"EMS production-line energy mapping verification failed for source id {sourceLine.Id}.");
            }
        }
    }

    private static int? ResolveTransformerId(
        EmsZoneSourceDefinition zone,
        IReadOnlyDictionary<int, Transformer> transformers)
        => zone.TransformerId.HasValue
            ? transformers[zone.TransformerId.Value].Id
            : null;
}

public sealed record EmsDefinitionSyncResult(
    int Factories,
    int Transformers,
    int Zones,
    int Lines);
