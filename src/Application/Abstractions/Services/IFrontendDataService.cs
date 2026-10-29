using Application.Dtos.Frontend;

namespace Application.Abstractions.Services;

public interface IFrontendDataService
{
    Task<object?> GetOverviewKpisAsyncV1(FrontendFilter? filter = null);

    Task<UiPageDto> GetOverviewAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetOperationsAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetBatchManagementAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetBatchDetailAsync(int batchId, FrontendFilter? filter = null);
    Task<object> GetBatchMaterialsAsync(int batchId, FrontendFilter? filter = null);
    Task<object> GetBatchStageLogAsync(int batchId, FrontendFilter? filter = null);
    Task<UiPageDto> GetPlanningAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetAssetsAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetAlarmsAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetAnalyticsAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetSkuManagementAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetConfigurationAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetHierarchyAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetMaintenanceAsync(FrontendFilter? filter = null);
    Task<UiPageDto> GetSignageAsync(FrontendFilter? filter = null);


    // Split frontend binding endpoints: every method below returns one UI section / one use-case-oriented read model.
    // Page-level methods above may still be used for initial page hydration, but frontend components should prefer these split endpoints.
    Task<object?> GetOverviewKpisAsync(FrontendFilter? filter = null);
    Task<object?> GetOverviewOeeTrendAsync(FrontendFilter? filter = null);
    Task<object?> GetOverviewProductionOutputAsync(FrontendFilter? filter = null);
    Task<object?> GetOverviewEnergyByMachineAsync(FrontendFilter? filter = null);
    Task<object?> GetOverviewActiveAlarmsAsync(FrontendFilter? filter = null);
    Task<object?> GetOverviewFactoriesProductionAsync(int? factoryId = null, int? lineId = null, FrontendFilter? filter = null);
    Task<object?> GetOverviewEnergyEfficiencyAsync(FrontendFilter? filter = null);
    Task<object?> GetOverviewEnergyBySkuAsync(FrontendFilter? filter = null);

    Task<object?> GetOperationsEnergyLoadAsync(FrontendFilter? filter = null);
    Task<object?> GetOperationsMachineUtilizationAsync(FrontendFilter? filter = null);
    Task<object?> GetOperationsMachineStatusTableAsync(FrontendFilter? filter = null);

    Task<object?> GetBatchUidLegendAsync(FrontendFilter? filter = null);
    Task<object?> GetBatchTableAsync(FrontendFilter? filter = null);
    Task<object?> GetBatchKanbanAsync(FrontendFilter? filter = null);
    Task<object?> GetBatchDetailDtoAsync(int batchId, FrontendFilter? filter = null);
    Task<object?> GetBatchHistoryAsync(FrontendFilter? filter = null);
    Task<object?> GetBatchHistoryDetailAsync(int batchId, FrontendFilter? filter = null);

    Task<object?> GetPlanningGanttAsync(FrontendFilter? filter = null);
    Task<object?> GetPlanningRoutesAsync(FrontendFilter? filter = null);
    Task<object?> GetPlanningMrpAsync(FrontendFilter? filter = null);
    Task<object?> GetPlanningStoppagesAsync(FrontendFilter? filter = null);

    Task<object?> GetAssetsKpisAsync(FrontendFilter? filter = null);
    Task<object?> GetAssetsTableAsync(FrontendFilter? filter = null);

    Task<object?> GetAlarmsSummaryAsync(FrontendFilter? filter = null);
    Task<object?> GetActiveAlarmsAsync(FrontendFilter? filter = null);
    Task<object?> GetUserAlarmRulesAsync(FrontendFilter? filter = null);
    Task<object?> GetSystemAlarmRulesAsync(FrontendFilter? filter = null);

    Task<object?> GetAnalyticsInsightsAsync(FrontendFilter? filter = null);
    Task<object?> GetAnalyticsOeeBreakdownAsync(FrontendFilter? filter = null);
    Task<object?> GetAnalyticsHeatmapAsync(FrontendFilter? filter = null);
    Task<object?> GetAnalyticsFailureRiskAsync(FrontendFilter? filter = null);

    Task<object?> GetSkuErpBannerAsync(FrontendFilter? filter = null);
    Task<object?> GetSkuListAsync(FrontendFilter? filter = null);
    Task<object?> GetMaterialListAsync(FrontendFilter? filter = null);
    Task<object?> GetBomListAsync(FrontendFilter? filter = null);
    Task<object?> GetSkuBomAsync(int skuId, FrontendFilter? filter = null);

    Task<object?> GetConfigurationGeneralSettingsAsync(FrontendFilter? filter = null);
    Task<object?> GetConfigurationShiftsAsync(FrontendFilter? filter = null);
    Task<object?> GetConfigurationMachineParametersAsync(FrontendFilter? filter = null);
    Task<object?> GetConfigurationColorCodesAsync(FrontendFilter? filter = null);
    Task<object?> GetConfigurationRatingThresholdsAsync(FrontendFilter? filter = null);

    Task<object?> GetHierarchyStatsAsync(FrontendFilter? filter = null);
    Task<object?> GetHierarchyTreeAsync(FrontendFilter? filter = null);

    Task<object?> GetMaintenanceKpisAsync(FrontendFilter? filter = null);
    Task<object?> GetMaintenanceWorkOrdersAsync(FrontendFilter? filter = null);
    Task<object?> GetMaintenancePmSchedulesAsync(FrontendFilter? filter = null);
    Task<object?> GetMaintenanceBreakdownHistoryAsync(FrontendFilter? filter = null);
    Task<object?> GetMaintenanceSparePartsAsync(FrontendFilter? filter = null);

    Task<object?> GetSignageScreensAsync(FrontendFilter? filter = null);
    Task<object?> GetSignageAndonBoardAsync(FrontendFilter? filter = null);
    Task<object?> GetSignageKpiDisplayConfigAsync(FrontendFilter? filter = null);
    Task<object?> GetSignageTemplatesAsync(FrontendFilter? filter = null);
    Task<IReadOnlyList<UseCaseCoverageDto>> GetUseCaseCoverageAsync();
}
