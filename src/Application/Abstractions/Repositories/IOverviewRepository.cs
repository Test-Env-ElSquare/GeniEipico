using Application.Dtos.Overview;
using Domain.Entities.Analytics;
using Domain.Entities.Alarms;

namespace Application.Abstractions.Repositories;

public interface IOverviewRepository
{
    Task<IReadOnlyList<OverviewMachine>> MachinesAsync(int customerId, int factoryId, int[] lineIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<OeeSnapshot>> SnapshotsAsync(OverviewPartition partition, CancellationToken cancellationToken);
    Task<IReadOnlyList<OeeSnapshotsPerHour>> HourlySnapshotsAsync(OverviewPartition partition, CancellationToken cancellationToken);
    Task<IReadOnlyList<AlarmEvent>> AlarmsAsync(OverviewPartition partition, CancellationToken cancellationToken);
}
