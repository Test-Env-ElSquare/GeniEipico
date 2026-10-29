using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Application.Dtos.Overview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Frontend;

[ApiController]
[Route("v1/ui")]
public sealed class UiController : ControllerBase
{
    private readonly IFrontendDataService _frontend;
    private readonly ITimeUtilities _timeUtilities;
    private readonly IBatchWorkflowService _workflow;
    private readonly IOverviewService _overview;

    public UiController(IFrontendDataService frontend, ITimeUtilities timeUtilities, IBatchWorkflowService workflow, IOverviewService overview)
    {
        _frontend = frontend;
        _timeUtilities = timeUtilities;
        _workflow = workflow;
        _overview = overview;
    }

    private async Task<FrontendFilter> ResolveFilterAsync(FrontendFilterQuery query)
    {
        var resolved = FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
        return resolved with { Search = query.Search, Status = query.Status };
    }

    // Page aggregate endpoints are kept for initial page hydration only.
    // Frontend components should prefer the split endpoints below, because each endpoint maps to one UI section / one use-case group.



    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("overview")]
    [Authorize]
    public async Task<IActionResult> Overview([FromQuery] FrontendFilterQuery query)
    {
        return await OverviewResponse(OverviewSection.Page, query);
    }

    //[HttpGet("overview/kpis")]
    //public async Task<IActionResult> OverviewKpis([FromQuery] FrontendFilterQuery query)
    //{
    //    var filter = await ResolveFilterAsync(query);
    //    return Ok(ApiResponse<object?>.Ok(await _frontend.GetOverviewKpisAsync(filter), filter));
    //}
    [HttpGet("overview/kpis")]
    [Authorize]
    public async Task<IActionResult> OverviewKpis([FromQuery] FrontendFilterQuery query)
    {
        return await OverviewResponse(OverviewSection.Kpis, query);
    }

    [HttpGet("overview/oee-trend")]
    [Authorize]
    public async Task<IActionResult> OverviewOeeTrend([FromQuery] FrontendFilterQuery query)
    {
        return await OverviewResponse(OverviewSection.OeeTrend, query);
    }

    [HttpGet("overview/production-output")]
    [Authorize]
    public async Task<IActionResult> OverviewProductionOutput([FromQuery] FrontendFilterQuery query)
    {
        return await OverviewResponse(OverviewSection.ProductionOutput, query);
    }

    [HttpGet("overview/energy-by-machine")]
    [Authorize]
    public async Task<IActionResult> OverviewEnergyByMachine([FromQuery] FrontendFilterQuery query)
    {
        return await OverviewResponse(OverviewSection.EnergyByMachine, query);
    }

    [HttpGet("overview/active-alarms")]
    [Authorize]
    public async Task<IActionResult> OverviewActiveAlarms([FromQuery] FrontendFilterQuery query)
    {
        return await OverviewResponse(OverviewSection.ActiveAlarms, query);
    }

    //[HttpGet("overview/factories-production")]
    //public async Task<IActionResult> OverviewFactoriesProduction(int factoryId , int lineId , [FromQuery] FrontendFilterQuery query)
    //{
    //    var filter = await ResolveFilterAsync(query);
    //    return Ok(ApiResponse<object?>.Ok(await _frontend.GetOverviewFactoriesProductionAsync(factoryId, lineId, filter), filter));
    //}
    [HttpGet("overview/factories-production")]
    [Authorize]
    public async Task<IActionResult> OverviewFactoriesProduction([FromQuery] FrontendFilterQuery query)
    {
        return await OverviewResponse(OverviewSection.FactoriesProduction, query);
    }
    [HttpGet("overview/energy-efficiency")]
    [Authorize]
    public async Task<IActionResult> OverviewEnergyEfficiency([FromQuery] FrontendFilterQuery query)
    {
        return await OverviewResponse(OverviewSection.EnergyEfficiency, query);
    }

    [HttpGet("overview/energy-by-sku")]
    [Authorize]
    public async Task<IActionResult> OverviewEnergyBySku([FromQuery] FrontendFilterQuery query)
    {
        return await OverviewResponse(OverviewSection.EnergyBySku, query);
    }

    private async Task<IActionResult> OverviewResponse(OverviewSection section, FrontendFilterQuery query)
    {
        var result = await _overview.ReadAsync(section, new OverviewQuery(query.Duration ?? 0, query.FactoryId, query.LineId,
            query.From, query.To), HttpContext.RequestAborted);
        return result.ErrorCode == null ? Ok(ApiResponse<object?>.Ok(result.Data, result.Meta))
            : StatusCode(result.StatusCode, ApiResponse<object>.Fail(result.ErrorCode, result.ErrorMessage!));
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("operations")]
    public async Task<IActionResult> Operations()
        => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetOperationsAsync()));

    [HttpGet("operations/energy-load")]
    public async Task<IActionResult> OperationsEnergyLoad()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetOperationsEnergyLoadAsync()));

    [HttpGet("operations/machine-utilization")]
    public async Task<IActionResult> OperationsMachineUtilization()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetOperationsMachineUtilizationAsync()));

    [HttpGet("operations/machine-status-table")]
    public async Task<IActionResult> OperationsMachineStatusTable()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetOperationsMachineStatusTableAsync()));

    //[ApiExplorerSettings(IgnoreApi = true)]
    //[HttpGet("batches")]
    //public async Task<IActionResult> BatchManagement([FromQuery] FrontendFilterQuery query)
    //{
    //    var filter = await ResolveFilterAsync(query);
    //    return Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetBatchManagementAsync(filter), filter));
    //}

    //[HttpGet("batches/uid-legend")]
    //public async Task<IActionResult> BatchUidLegend([FromQuery] FrontendFilterQuery query)
    //{
    //    var filter = await ResolveFilterAsync(query);
    //    return Ok(ApiResponse<object?>.Ok(await _frontend.GetBatchUidLegendAsync(filter), filter));
    //}

    //[HttpGet("batches/table")]
    //public async Task<IActionResult> BatchTable([FromQuery] FrontendFilterQuery query)
    //{
    //    var filter = await ResolveFilterAsync(query);
    //    return Ok(ApiResponse<object?>.Ok(await _frontend.GetBatchTableAsync(filter), filter));
    //}

    //[HttpGet("batches/kanban")]
    //public async Task<IActionResult> BatchKanban([FromQuery] FrontendFilterQuery query)
    //{
    //    var filter = await ResolveFilterAsync(query);
    //    return Ok(ApiResponse<object?>.Ok(await _frontend.GetBatchKanbanAsync(filter), filter));
    //}

    //[HttpGet("batches/{id:int}/detail")]
    //public async Task<IActionResult> BatchDetail(int id)
    //{
    //    var detail = await _frontend.GetBatchDetailDtoAsync(id);
    //    return detail is null
    //        ? NotFound(ApiResponse<object>.Fail("not_found", $"Batch {id} was not found."))
    //        : Ok(ApiResponse<object?>.Ok(detail));
    //}

    //[HttpGet("batches/history")]
    //public async Task<IActionResult> BatchHistory([FromQuery] FrontendFilterQuery query)
    //{
    //    var filter = await ResolveFilterAsync(query);
    //    return Ok(ApiResponse<object?>.Ok(await _frontend.GetBatchHistoryAsync(filter), filter));
    //}

    //[HttpGet("batches/{id:int}/history-detail")]
    //public async Task<IActionResult> BatchHistoryDetail(int id, [FromQuery] FrontendFilterQuery query)
    //{
    //    var filter = await ResolveFilterAsync(query);
    //    var detail = await _frontend.GetBatchHistoryDetailAsync(id, filter);
    //    return detail is null
    //        ? NotFound(ApiResponse<object>.Fail("not_found", $"Batch {id} was not found."))
    //        : Ok(ApiResponse<object?>.Ok(detail, filter));
    //}

    //[HttpGet("batches/preview-uid")]
    //public async Task<IActionResult> PreviewUid([FromQuery] int customerId)
    //{
    //    var preview = await _workflow.GetPreviewBatchUidAsync(customerId);
    //    return Ok(ApiResponse<string>.Ok(preview));
    //}

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("planning")]
    public async Task<IActionResult> Planning()
        => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetPlanningAsync()));

    [HttpGet("planning/gantt")]
    public async Task<IActionResult> PlanningGantt()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetPlanningGanttAsync()));

    [HttpGet("planning/routing")]
    public async Task<IActionResult> PlanningRouting()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetPlanningRoutesAsync()));


    [HttpGet("planning/mrp")]
    public async Task<IActionResult> PlanningMrp()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetPlanningMrpAsync()));

    [HttpGet("planning/stoppages")]
    public async Task<IActionResult> PlanningStoppages()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetPlanningStoppagesAsync()));

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("assets")]
    public async Task<IActionResult> Assets()
        => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetAssetsAsync()));

    [HttpGet("assets/kpis")]
    public async Task<IActionResult> AssetsKpis()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetAssetsKpisAsync()));

    [HttpGet("assets/table")]
    public async Task<IActionResult> AssetsTable()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetAssetsTableAsync()));

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("alarms")]
    public async Task<IActionResult> Alarms()
        => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetAlarmsAsync()));

    [HttpGet("alarms/summary")]
    public async Task<IActionResult> AlarmsSummary()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetAlarmsSummaryAsync()));

    [HttpGet("alarms/active")]
    public async Task<IActionResult> ActiveAlarms()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetActiveAlarmsAsync()));

    [HttpGet("alarms/user-rules")]
    public async Task<IActionResult> UserAlarmRules()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetUserAlarmRulesAsync()));

    [HttpGet("alarms/system-rules")]
    public async Task<IActionResult> SystemAlarmRules()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetSystemAlarmRulesAsync()));

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics()
        => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetAnalyticsAsync()));

    [HttpGet("analytics/insights")]
    public async Task<IActionResult> AnalyticsInsights()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetAnalyticsInsightsAsync()));

    //[HttpGet("analytics/oee-breakdown")]
    //public async Task<IActionResult> AnalyticsOeeBreakdown()
    //    => Ok(ApiResponse<object?>.Ok(await _frontend.GetAnalyticsOeeBreakdownAsync()));

    //[HttpGet("analytics/heatmap")]
    //public async Task<IActionResult> AnalyticsHeatmap()
    //    => Ok(ApiResponse<object?>.Ok(await _frontend.GetAnalyticsHeatmapAsync()));

    [HttpGet("analytics/failure-risk")]
    public async Task<IActionResult> AnalyticsFailureRisk()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetAnalyticsFailureRiskAsync()));

    [ApiExplorerSettings(IgnoreApi = true)]
    //[HttpGet("sku-management")]
    //public async Task<IActionResult> SkuManagement()
    //    => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetSkuManagementAsync()));

    //[HttpGet("sku-management/erp-banner")]
    //public async Task<IActionResult> SkuErpBanner()
    //    => Ok(ApiResponse<object?>.Ok(await _frontend.GetSkuErpBannerAsync()));

    //[HttpGet("sku-management/skus")]
    //public async Task<IActionResult> SkuList()
    //    => Ok(ApiResponse<object?>.Ok(await _frontend.GetSkuListAsync()));

    //[HttpGet("sku-management/materials")]
    //public async Task<IActionResult> MaterialList()
    //    => Ok(ApiResponse<object?>.Ok(await _frontend.GetMaterialListAsync()));

    //[HttpGet("sku-management/bom")]
    //public async Task<IActionResult> BomList()
    //    => Ok(ApiResponse<object?>.Ok(await _frontend.GetBomListAsync()));

    //[HttpGet("sku-management/skus/{skuId:int}/bom")]
    //public async Task<IActionResult> SkuBom(int skuId)
    //    => Ok(ApiResponse<object?>.Ok(await _frontend.GetSkuBomAsync(skuId)));

    //[ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("configuration")]
    public async Task<IActionResult> Configuration()
        => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetConfigurationAsync()));

    [HttpGet("configuration/general-settings")]
    public async Task<IActionResult> ConfigurationGeneralSettings()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetConfigurationGeneralSettingsAsync()));

    [HttpGet("configuration/shifts")]
    public async Task<IActionResult> ConfigurationShifts()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetConfigurationShiftsAsync()));

    [HttpGet("configuration/machine-parameters")]
    public async Task<IActionResult> ConfigurationMachineParameters()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetConfigurationMachineParametersAsync()));

    [HttpGet("configuration/color-codes")]
    public async Task<IActionResult> ConfigurationColorCodes()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetConfigurationColorCodesAsync()));

    [HttpGet("configuration/rating-thresholds")]
    public async Task<IActionResult> ConfigurationRatingThresholds()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetConfigurationRatingThresholdsAsync()));

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("hierarchy")]
    public async Task<IActionResult> Hierarchy()
        => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetHierarchyAsync()));

    [HttpGet("hierarchy/stats")]
    public async Task<IActionResult> HierarchyStats()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetHierarchyStatsAsync()));

    [HttpGet("hierarchy/tree")]
    public async Task<IActionResult> HierarchyTree()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetHierarchyTreeAsync()));

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("maintenance")]
    public async Task<IActionResult> Maintenance()
        => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetMaintenanceAsync()));

    [HttpGet("maintenance/kpis")]
    public async Task<IActionResult> MaintenanceKpis()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetMaintenanceKpisAsync()));

    [HttpGet("maintenance/work-orders")]
    public async Task<IActionResult> MaintenanceWorkOrders()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetMaintenanceWorkOrdersAsync()));

    [HttpGet("maintenance/pm-schedules")]
    public async Task<IActionResult> MaintenancePmSchedules()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetMaintenancePmSchedulesAsync()));

    [HttpGet("maintenance/breakdown-history")]
    public async Task<IActionResult> MaintenanceBreakdownHistory()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetMaintenanceBreakdownHistoryAsync()));

    [HttpGet("maintenance/spare-parts")]
    public async Task<IActionResult> MaintenanceSpareParts()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetMaintenanceSparePartsAsync()));

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("signage")]
    public async Task<IActionResult> Signage()
        => Ok(ApiResponse<UiPageDto>.Ok(await _frontend.GetSignageAsync()));

    [HttpGet("signage/screens")]
    public async Task<IActionResult> SignageScreens()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetSignageScreensAsync()));

    [HttpGet("signage/andon-board")]
    public async Task<IActionResult> SignageAndonBoard()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetSignageAndonBoardAsync()));

    [HttpGet("signage/kpi-display-config")]
    public async Task<IActionResult> SignageKpiDisplayConfig()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetSignageKpiDisplayConfigAsync()));

    [HttpGet("signage/templates")]
    public async Task<IActionResult> SignageTemplates()
        => Ok(ApiResponse<object?>.Ok(await _frontend.GetSignageTemplatesAsync()));

    [HttpGet("use-case-coverage")]
    public async Task<IActionResult> UseCaseCoverage()
        => Ok(ApiResponse<IReadOnlyList<UseCaseCoverageDto>>.Ok(await _frontend.GetUseCaseCoverageAsync()));
}
