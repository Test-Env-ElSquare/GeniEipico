using Application.Abstractions.Repositories;
using Application.Dtos.Overview;
using Domain.Entities.Alarms;
using Domain.Entities.Analytics;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repository;

public sealed class OverviewRepository(MesDbContext db) : IOverviewRepository
{
    public async Task<IReadOnlyList<OverviewMachine>> MachinesAsync(int customerId, int factoryId, int[] lineIds, CancellationToken cancellationToken)
        => await db.Machines.AsNoTracking().Where(m => lineIds.Contains(m.LineId) && m.Line.FactoryId == factoryId &&
                m.Line.Factory.CustomerId == customerId)
            .Select(m => new OverviewMachine(m.Id, m.Name, m.LineId, m.Line.Name, m.Line.FactoryId, m.Line.Factory.Name))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<OeeSnapshot>> SnapshotsAsync(OverviewPartition p, CancellationToken cancellationToken)
    {
        // ShiftDate is a local shift start for Batterjee. ComputedAt is the save time, not the production period.
        var from = p.Period.From.AddHours(p.SnapshotUtcOffsetHours);
        var to = p.Period.To.AddHours(p.SnapshotUtcOffsetHours);
        return await db.OeeSnapshots.AsNoTracking().Where(x =>
            // Enforce both the recorded scope and the machine hierarchy. Legacy null scope fields use the hierarchy.
            (x.CustomerId == null || x.CustomerId == p.CustomerId) && (x.FactoryId == null || x.FactoryId == p.FactoryId) &&
            (x.LineId == null || p.LineIds.Contains(x.LineId.Value)) &&
            x.Machine.Line.Factory.CustomerId == p.CustomerId && x.Machine.Line.FactoryId == p.FactoryId &&
            p.LineIds.Contains(x.Machine.LineId) && x.ShiftDate >= from && x.ShiftDate < to)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OeeSnapshotsPerHour>> HourlySnapshotsAsync(OverviewPartition p, CancellationToken cancellationToken)
    {
        var from = p.Period.From.AddHours(p.SnapshotUtcOffsetHours);
        var to = p.Period.To.AddHours(p.SnapshotUtcOffsetHours);
        return await db.OeeSnapshotsPerHour.AsNoTracking().Where(x =>
            (x.CustomerId == null || x.CustomerId == p.CustomerId) && (x.FactoryId == null || x.FactoryId == p.FactoryId) &&
            (x.LineId == null || p.LineIds.Contains(x.LineId.Value)) &&
            x.Machine.Line.Factory.CustomerId == p.CustomerId && x.Machine.Line.FactoryId == p.FactoryId &&
            p.LineIds.Contains(x.Machine.LineId) && x.ShiftDate >= from && x.ShiftDate < to)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlarmEvent>> AlarmsAsync(OverviewPartition p, CancellationToken cancellationToken)
    {
        var factoryWide = p.IncludeFactoryWide && !await db.ProductionLines.AnyAsync(x => x.FactoryId == p.FactoryId &&
            x.IsActive && !p.LineIds.Contains(x.Id), cancellationToken);
        return await db.AlarmEvents.AsNoTracking().Where(x => x.CustomerId == p.CustomerId && x.ResolvedAt == null &&
            x.TriggeredAt >= p.Period.From && x.TriggeredAt < p.Period.To && (
                x.MachineId != null && x.Machine!.Line.FactoryId == p.FactoryId && p.LineIds.Contains(x.Machine.LineId) ||
                x.MachineId == null && x.SiloId != null && factoryWide && x.Silo!.FactoryId == p.FactoryId ||
                x.MachineId == null && x.SiloId == null && x.DeviceId != null && x.Device!.FactoryId == p.FactoryId &&
                    (x.Device.LineId != null && p.LineIds.Contains(x.Device.LineId.Value) || x.Device.LineId == null && factoryWide)))
            .OrderByDescending(x => x.TriggeredAt).ToArrayAsync(cancellationToken);
    }
}
