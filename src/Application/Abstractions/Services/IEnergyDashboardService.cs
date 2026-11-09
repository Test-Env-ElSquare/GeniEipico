using Application.Dtos.EnergyDashboard;

namespace Application.Abstractions.Services;

public sealed record EnergyDashboardScope(
    IReadOnlyCollection<int> FactoryIds,
    int? SelectedFactoryId,
    int ResponseFactoryId);

public sealed record EnergyDashboardFilter(
    EnergyDashboardScope Scope,
    int Duration = 0,
    int? TransformerId = null,
    int? ZoneId = null,
    DateTime? From = null,
    DateTime? To = null);

public interface IEnergyDashboardService
{
    Task<TransformerSummaryDto> GetSummaryAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default);

    Task<OnlineStatusDto> GetOnlineStatusAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnergyHeatmapDto>> GetEnergyHeatmapAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HourlyEnergyConsumptionDto>> GetHourlyEnergyAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnergyConsumptionDto>> GetTotalEnergyAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HarmonicsGraphDto>> GetHarmonicsGraphAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VoltageGraphDto>> GetVoltageGraphAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CurrentGraphDto>> GetCurrentGraphAsync(
        EnergyDashboardFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopEnergyConsumerDto>> GetTopEnergyConsumersAsync(
        EnergyDashboardFilter filter,
        int top,
        CancellationToken cancellationToken = default);
}
