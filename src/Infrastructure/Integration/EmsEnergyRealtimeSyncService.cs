using Domain.Entities.Analytics;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Integration;

public sealed class EmsEnergyRealtimeSyncService(
    MesDbContext db,
    EmsEnergySourceReader source,
    EmsEnergySourceTime sourceTime)
{
    public const string SourceSystem = "EMS";
    public const string StreamName = "Energy";

    /// <summary>
    /// Creates the first checkpoint at the current source maximum. This is the
    /// explicit no-history boundary: rows at or below it are never backfilled.
    /// </summary>
    public async Task<EmsEnergyCheckpointResult> EnsureLiveCheckpointAsync(
        CancellationToken cancellationToken)
    {
        var checkpoint = await db.SyncCheckpoints.SingleOrDefaultAsync(
            x => x.SourceSystem == SourceSystem && x.StreamName == StreamName,
            cancellationToken);

        if (checkpoint is not null)
            return new EmsEnergyCheckpointResult(checkpoint.LastSourceId, false);

        var liveStartId = await source.GetMaxEnergyIdAsync(cancellationToken);
        checkpoint = new Domain.Entities.Integration.SyncCheckpoint
        {
            SourceSystem = SourceSystem,
            StreamName = StreamName,
            LastSourceId = liveStartId,
            UpdatedAt = DateTime.UtcNow
        };
        db.SyncCheckpoints.Add(checkpoint);
        await db.SaveChangesAsync(cancellationToken);
        return new EmsEnergyCheckpointResult(liveStartId, true);
    }

    public async Task<EmsEnergyPageSyncResult> SyncNextPageAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));

        var checkpointSnapshot = await db.SyncCheckpoints.AsNoTracking().SingleAsync(
            x => x.SourceSystem == SourceSystem && x.StreamName == StreamName,
            cancellationToken);

        var records = await source.ReadEnergyAfterAsync(
            checkpointSnapshot.LastSourceId,
            batchSize,
            cancellationToken);
        if (records.Count == 0)
            return new EmsEnergyPageSyncResult(0, checkpointSnapshot.LastSourceId);

        var transformers = await db.Transformers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);
        var transformersByName = transformers.ToDictionary(
            x => x.TransformerName,
            StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (!transformersByName.ContainsKey(record.SourceId))
            {
                throw new InvalidDataException(
                    $"EMS Energy row {record.Id} references unknown existing transformer '{record.SourceId}'.");
            }
        }

        var factoryIds = transformers.Select(x => x.FactoryId).Distinct().ToArray();
        var factories = await db.Factories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => factoryIds.Contains(x.Id) && !x.IsDeleted && x.IsActive)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var missingFactory = transformers.FirstOrDefault(x => !factories.ContainsKey(x.FactoryId));
        if (missingFactory is not null)
        {
            throw new InvalidDataException(
                $"Existing transformer '{missingFactory.TransformerName}' has no active factory.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var checkpoint = await db.SyncCheckpoints.SingleAsync(
                x => x.SourceSystem == SourceSystem && x.StreamName == StreamName,
                cancellationToken);
            if (checkpoint.LastSourceId != checkpointSnapshot.LastSourceId)
            {
                throw new InvalidOperationException(
                    "EMS Energy checkpoint changed during a synchronization page.");
            }

            var sourceIds = records.Select(x => (long)x.Id).ToArray();
            var existingBySourceId = await db.EnergyReadings
                .Where(x => x.EmsSourceRecordId.HasValue &&
                            sourceIds.Contains(x.EmsSourceRecordId.Value))
                .ToDictionaryAsync(x => x.EmsSourceRecordId!.Value, cancellationToken);

            var mappedRecords = records.Select(record =>
            {
                var transformer = transformersByName[record.SourceId];
                return new MappedSource(
                    record,
                    transformer,
                    factories[transformer.FactoryId],
                    sourceTime.ToUtc(record.TimeStamp),
                    sourceTime.ToUtc(record.ShiftStartTime));
            }).ToList();

            var transformerIds = mappedRecords.Select(x => x.Transformer.Id).Distinct().ToArray();
            var minTimestamp = mappedRecords.Min(x => x.TimestampUtc);
            var maxTimestamp = mappedRecords.Max(x => x.TimestampUtc);
            var existingByNaturalKey = (await db.EnergyReadings
                    .Where(x => transformerIds.Contains(x.TransformerId) &&
                                x.Timestamp >= minTimestamp &&
                                x.Timestamp <= maxTimestamp)
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => (x.TransformerId, x.Timestamp));

            foreach (var mapped in mappedRecords)
            {
                if (!existingBySourceId.TryGetValue(mapped.Source.Id, out var target) &&
                    !existingByNaturalKey.TryGetValue(
                        (mapped.Transformer.Id, mapped.TimestampUtc),
                        out target))
                {
                    target = new EnergyReading();
                    db.EnergyReadings.Add(target);
                }

                Apply(mapped, target);
            }

            checkpoint.LastSourceId = records[^1].Id;
            checkpoint.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var persisted = await db.EnergyReadings
                .AsNoTracking()
                .Where(x => x.EmsSourceRecordId.HasValue &&
                            sourceIds.Contains(x.EmsSourceRecordId.Value))
                .ToDictionaryAsync(x => x.EmsSourceRecordId!.Value, cancellationToken);
            foreach (var mapped in mappedRecords)
            {
                if (!persisted.TryGetValue(mapped.Source.Id, out var target) ||
                    !MappedValuesEqual(mapped, target))
                {
                    throw new InvalidDataException(
                        $"EMS realtime verification failed for Energy id {mapped.Source.Id}.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new EmsEnergyPageSyncResult(records.Count, checkpoint.LastSourceId);
        });
    }

    private static void Apply(MappedSource mapped, EnergyReading target)
    {
        var source = mapped.Source;
        target.EmsSourceRecordId = source.Id;
        target.CustomerId = mapped.Factory.CustomerId;
        target.FactoryId = mapped.Factory.Id;
        target.LineId = null;
        target.TransformerId = mapped.Transformer.Id;
        target.Quality = source.Fault is >= short.MinValue and <= short.MaxValue
            ? (short?)source.Fault.Value
            : null;
        target.VoltageL1 = source.V1;
        target.VoltageL2 = source.V2;
        target.VoltageL3 = source.V3;
        target.CurrentL1 = source.I1;
        target.CurrentL2 = source.I2;
        target.CurrentL3 = source.I3;
        target.PowerFactorL1 = source.PF1;
        target.PowerFactorL2 = source.PF2;
        target.PowerFactorL3 = source.PF3;
        target.ApparentPowerL1 = source.Papp1;
        target.ApparentPowerL2 = source.Papp2;
        target.ApparentPowerL3 = source.Papp3;
        target.ActivePowerL1 = source.Pact1;
        target.ActivePowerL2 = source.Pact2;
        target.ActivePowerL3 = source.Pact3;
        target.ReactivePowerL1 = source.Preact1;
        target.ReactivePowerL2 = source.Preact2;
        target.ReactivePowerL3 = source.Preact3;
        target.EnergyL1 = source.Energy1;
        target.EnergyL2 = source.Energy2;
        target.EnergyL3 = source.Energy3;
        target.EnergyDiffL1 = source.DiffEnergy1;
        target.EnergyDiffL2 = source.DiffEnergy2;
        target.EnergyDiffL3 = source.DiffEnergy3;
        target.TotalEnergyConsumptionDiff = source.TotalEnergyConsumptionDiff;
        target.VoltageThdL1 = source.THDv1;
        target.VoltageThdL2 = source.THDv2;
        target.VoltageThdL3 = source.THDv3;
        target.CurrentThdL1 = source.THDi1;
        target.CurrentThdL2 = source.THDi2;
        target.CurrentThdL3 = source.THDi3;
        target.Timestamp = mapped.TimestampUtc;
        target.ShiftStartTime = mapped.ShiftStartTimeUtc;
    }

    private static bool MappedValuesEqual(MappedSource mapped, EnergyReading target)
    {
        var expected = new EnergyReading();
        Apply(mapped, expected);
        return target.EmsSourceRecordId == expected.EmsSourceRecordId &&
               target.CustomerId == expected.CustomerId &&
               target.FactoryId == expected.FactoryId &&
               target.LineId == expected.LineId &&
               target.TransformerId == expected.TransformerId &&
               target.Quality == expected.Quality &&
               target.VoltageL1 == expected.VoltageL1 &&
               target.VoltageL2 == expected.VoltageL2 &&
               target.VoltageL3 == expected.VoltageL3 &&
               target.CurrentL1 == expected.CurrentL1 &&
               target.CurrentL2 == expected.CurrentL2 &&
               target.CurrentL3 == expected.CurrentL3 &&
               target.PowerFactorL1 == expected.PowerFactorL1 &&
               target.PowerFactorL2 == expected.PowerFactorL2 &&
               target.PowerFactorL3 == expected.PowerFactorL3 &&
               target.ApparentPowerL1 == expected.ApparentPowerL1 &&
               target.ApparentPowerL2 == expected.ApparentPowerL2 &&
               target.ApparentPowerL3 == expected.ApparentPowerL3 &&
               target.ActivePowerL1 == expected.ActivePowerL1 &&
               target.ActivePowerL2 == expected.ActivePowerL2 &&
               target.ActivePowerL3 == expected.ActivePowerL3 &&
               target.ReactivePowerL1 == expected.ReactivePowerL1 &&
               target.ReactivePowerL2 == expected.ReactivePowerL2 &&
               target.ReactivePowerL3 == expected.ReactivePowerL3 &&
               target.EnergyL1 == expected.EnergyL1 &&
               target.EnergyL2 == expected.EnergyL2 &&
               target.EnergyL3 == expected.EnergyL3 &&
               target.EnergyDiffL1 == expected.EnergyDiffL1 &&
               target.EnergyDiffL2 == expected.EnergyDiffL2 &&
               target.EnergyDiffL3 == expected.EnergyDiffL3 &&
               target.TotalEnergyConsumptionDiff == expected.TotalEnergyConsumptionDiff &&
               target.VoltageThdL1 == expected.VoltageThdL1 &&
               target.VoltageThdL2 == expected.VoltageThdL2 &&
               target.VoltageThdL3 == expected.VoltageThdL3 &&
               target.CurrentThdL1 == expected.CurrentThdL1 &&
               target.CurrentThdL2 == expected.CurrentThdL2 &&
               target.CurrentThdL3 == expected.CurrentThdL3 &&
               target.Timestamp == expected.Timestamp &&
               target.ShiftStartTime == expected.ShiftStartTime;
    }

    private sealed record MappedSource(
        EmsEnergySourceRecord Source,
        Transformer Transformer,
        Factory Factory,
        DateTime TimestampUtc,
        DateTime ShiftStartTimeUtc);
}

public sealed record EmsEnergyCheckpointResult(long LastSourceId, bool InitializedNow);
public sealed record EmsEnergyPageSyncResult(int Count, long LastSourceId);
