using Application.Dtos.Overview;
using Application.Common.Time;

namespace Application.Abstractions.Services;

public interface IOverviewAccessProvider
{
    OverviewAccess? GetAccess();
}
public interface IOverviewService
{
    Task<OverviewResult> ReadAsync(OverviewSection section, OverviewQuery query, CancellationToken cancellationToken = default);
}
public interface IFactorySnapshotService
{
    Task<FactorySnapshotReadResult> ReadCurrentAsync(ScadaLiveViewScope scope, CancellationToken cancellationToken = default);
}
public interface IOverviewLiveProvider
{
    bool Supports(int customerId, int factoryId);
    Task<OverviewLiveResult> ReadOeeAsync(int customerId, int factoryId, int[] lineIds, CancellationToken cancellationToken);
    Task<OverviewLiveResult> ReadHourlyOeeAsync(int customerId, int factoryId, int[] lineIds, CancellationToken cancellationToken);
}

public interface IOverviewPeriodResolver
{
    int SnapshotUtcOffsetHours(int customerId, int factoryId);
    Task<ResolvedTimeFilter> ResolveAsync(int customerId, TimeFilterInput input);
}
