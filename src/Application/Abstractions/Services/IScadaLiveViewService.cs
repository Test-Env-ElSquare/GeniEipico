using Application.Common.Time;

namespace Application.Abstractions.Services;

public sealed record ScadaLiveViewScope(
    int CustomerId,
    int FactoryId,
    int? LineId,
    IReadOnlySet<int> AllowedLineIds);

public sealed record ScadaLiveViewResult(
    object? Data,
    ResolvedTimeFilter CurrentShift);

public sealed record ScadaMachineTimelineSegment(
    string State,
    DateTime StartedAt,
    DateTime EndedAt,
    decimal DurationMinutes,
    decimal Percentage,
    bool IsCurrent);

public interface IScadaLiveViewService
{
    Task<ScadaLiveViewResult> GetDashboardAsync(ScadaLiveViewScope scope);
    Task<ScadaLiveViewResult> GetStatusCountsAsync(ScadaLiveViewScope scope);
    Task<ScadaLiveViewResult> GetWarehouseZoneAsync(ScadaLiveViewScope scope);
    Task<ScadaLiveViewResult> GetDispensingZoneAsync(ScadaLiveViewScope scope);
    Task<ScadaLiveViewResult> GetProductionZoneAsync(ScadaLiveViewScope scope);
    Task<ScadaLiveViewResult> GetFactoryLayoutAsync(ScadaLiveViewScope scope);
    Task<object?> GetMachineDetailAsync(
        ScadaLiveViewScope scope,
        int machineId,
        ResolvedTimeFilter period);
    IReadOnlyList<ScadaMachineTimelineSegment> GetMachineTimeline(
        ScadaLiveViewScope scope,
        int machineId,
        ResolvedTimeFilter period);
    Task<ScadaLiveViewResult> GetFinishedGoodsZoneAsync(ScadaLiveViewScope scope);
    Task<ScadaLiveViewResult> GetEnergyAsync(ScadaLiveViewScope scope);
}
