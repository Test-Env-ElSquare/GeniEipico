using Application.Abstractions.Services;
using Application.Services;
using Application.Common.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(ICrudService<>), typeof(CrudService<>));
        services.AddScoped<IAiInsightService, AiInsightService>();
        services.AddScoped<IAlarmEventService, AlarmEventService>();
        services.AddScoped<IAlarmRuleService, AlarmRuleService>();
        services.AddScoped<IAlarmRuleEmailTargetService, AlarmRuleEmailTargetService>();
        services.AddScoped<IAlarmRuleSmsTargetService, AlarmRuleSmsTargetService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IBatchService, BatchService>();
        services.AddScoped<IBatchMaterialService, BatchMaterialService>();
        services.AddScoped<IBatchStageLogService, BatchStageLogService>();
        services.AddScoped<IBatchUidSequenceService, BatchUidSequenceService>();
        services.AddScoped<IBillOfMaterialService, BillOfMaterialService>();
        services.AddScoped<IBreakdownLogService, BreakdownLogService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDeviceTypeService, DeviceTypeService>();
        services.AddScoped<IDeviceTypeTagDefinitionService, DeviceTypeTagDefinitionService>();
        services.AddScoped<IFactoryService, FactoryService>();
        services.AddScoped<IMachineService, MachineService>();
        services.AddScoped<IMachineConfigService, MachineConfigService>();
        services.AddScoped<IMachineStateService, MachineStateService>();
        services.AddScoped<IMachineStateHistoryService, MachineStateHistoryService>();
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IMetricThresholdService, MetricThresholdService>();
        services.AddScoped<IMrpRequirementService, MrpRequirementService>();
        services.AddScoped<IOeeSnapshotService, OeeSnapshotService>();
        services.AddScoped<IPlatformSettingService, PlatformSettingService>();
        services.AddScoped<IPmScheduleService, PmScheduleService>();
        services.AddScoped<IProductionLineService, ProductionLineService>();
        services.AddScoped<IProductionRouteService, ProductionRouteService>();
        services.AddScoped<IProductionScheduleService, ProductionScheduleService>();
        services.AddScoped<IRouteStepService, RouteStepService>();
        services.AddScoped<IRouteStepParameterService, RouteStepParameterService>();
        services.AddScoped<IScaleService, ScaleService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<ISiloService, SiloService>();
        services.AddScoped<ISkuService, SkuService>();
        services.AddScoped<ISkuProcessParameterService, SkuProcessParameterService>();
        services.AddScoped<ISparePartService, SparePartService>();
        services.AddScoped<ISparePartMachineTypeService, SparePartMachineTypeService>();
        services.AddScoped<ITelemetryAggregateService, TelemetryAggregateService>();
        services.AddScoped<ITelemetryRawService, TelemetryRawService>();
        services.AddScoped<ITelemetryTagService, TelemetryTagService>();
        services.AddScoped<IWoPartUsedService, WoPartUsedService>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        services.AddScoped<IBatchWorkflowService, BatchWorkflowService>();
        services.AddScoped<IRouteWorkflowService, RouteWorkflowService>();
        services.AddScoped<IConfigWorkflowService, ConfigWorkflowService>();
        services.AddScoped<IMaintenanceWorkflowService, MaintenanceWorkflowService>();
        services.AddScoped<IAlarmWorkflowService, AlarmWorkflowService>();
        services.AddScoped<IAlarmEvaluationService, AlarmEvaluationService>();
        services.AddScoped<IScadaManualWorkflowService, ScadaManualWorkflowService>();
        services.AddScoped<IScadaLiveViewService, ScadaLiveViewService>();
        services.AddScoped<IFrontendDataService, FrontendDataService>();
        services.AddScoped<IOverviewService, OverviewService>();
        services.AddScoped<IFactorySnapshotService, FactorySnapshotService>();
        services.AddScoped<IOverviewPeriodResolver, OverviewPeriodResolver>();
        services.AddScoped<ITimeUtilities, TimeUtilities>();

        return services;
    }
}
