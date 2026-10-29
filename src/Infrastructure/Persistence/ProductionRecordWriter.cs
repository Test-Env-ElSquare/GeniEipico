using Application.Abstractions.Services;
using Application.Services;
using Domain.Entities.Scada;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// All Production writers share one append path. A transaction-owned SQL Server
/// lock serializes writes for a machine, including writes from other API instances.
/// No existing Production entity is attached for modification.
/// </summary>
public sealed class ProductionRecordWriter(DbContextOptions<MesDbContext> options) : IProductionRecordWriter
{
    public async Task<MachineState> AppendAsync(
        int machineId,
        MachineState reading,
        string source,
        string? notes = null,
        bool manualStateOnly = false,
        CancellationToken cancellationToken = default)
    {
        // Use a fresh context on every retry: a failed Save/Commit must not leave
        // Added entities or temporary/generated keys in the next attempt.
        await using var strategyContext = new MesDbContext(options);
        return await strategyContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var db = new MesDbContext(options);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var lockKey = $"RealTime.Production:machine:{machineId}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = {lockKey}, @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction', @LockTimeout = 10000;
                IF @result < 0 THROW 51000, 'Could not acquire the Production append lock.', 1;
                """, cancellationToken);

            var machine = await db.Machines.AsNoTracking()
                .SingleAsync(x => x.Id == machineId && x.IsActive && !x.IsDeleted, cancellationToken);
            var line = await db.ProductionLines.AsNoTracking()
                .SingleAsync(x => x.Id == machine.LineId && x.IsActive && !x.IsDeleted, cancellationToken);
            var factory = await db.Factories.AsNoTracking()
                .SingleAsync(x => x.Id == line.FactoryId, cancellationToken);
            var legacyId = machine.Id.ToString();
            var records = db.MachineStates.AsNoTracking().Where(x =>
                x.CustomerId == factory.CustomerId && x.FactoryId == factory.Id && x.LineId == line.Id &&
                (x.MachineId == machine.Name || x.MachineId == machine.Code || x.MachineId == legacyId));

            // The existing tag ingestion contract groups a signal by source name
            // and RecordedAt. Replaying that same signal must not append twice.
            // Manual commands are separate events, not telemetry replays.
            if (!manualStateOnly)
            {
                var existing = await records.Where(x => x.Timestamp == reading.Timestamp)
                    .OrderByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
                if (existing is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return existing;
                }
            }

            var previous = await records.Where(x => x.Timestamp <= reading.Timestamp)
                .OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var next = await records.Where(x => x.Timestamp > reading.Timestamp)
                .OrderBy(x => x.Timestamp).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            var record = ProductionRecordFactory.Create(reading, previous, manualStateOnly);
            // A later stored reading already accounted for this interval. Keep
            // the delayed sample, but don't double-count its partial increment.
            // Exact historical buckets can be reconstructed from Count + Timestamp.
            if (next is not null)
            {
                record.CountDiff = 0m;
                record.QRejectDiff = 0m;
            }
            record.MachineId = machine.Name;
            record.CustomerId = factory.CustomerId;
            record.FactoryId = factory.Id;
            record.LineId = line.Id;
            db.MachineStates.Add(record);

            var fromState = previous is null ? null : ProductionRecordFactory.StateLabel(previous);
            var toState = ProductionRecordFactory.StateLabel(record);
            await EnsureHistoryBoundaryAsync(db, machineId, record.Timestamp, fromState, toState,
                source, notes, cancellationToken);

            // A delayed signal is still appended, never discarded. Ensure the
            // following state is restored at its original event time as well.
            if (next is not null)
                await EnsureHistoryBoundaryAsync(db, machineId, next.Timestamp, toState,
                    ProductionRecordFactory.StateLabel(next), "production-history", null, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return record;
        });
    }

    private static async Task EnsureHistoryBoundaryAsync(
        MesDbContext db, int machineId, DateTime at, string? from, string to,
        string source, string? notes, CancellationToken cancellationToken)
    {
        var history = await db.MachineStateHistories
            .Where(x => x.MachineId == machineId && x.TransitionedAt == at)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (history is not null)
        {
            history.FromState = from;
            history.ToState = to;
        }
        else if (!string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            db.MachineStateHistories.Add(new MachineStateHistory
            {
                MachineId = machineId,
                FromState = from,
                ToState = to,
                TransitionedAt = at,
                TriggerSource = source,
                Notes = notes
            });
        }
    }
}
