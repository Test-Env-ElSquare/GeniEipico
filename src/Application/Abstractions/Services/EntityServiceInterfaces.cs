using Application.Abstractions.Services;
using Domain.Entities.Alarms;
using Domain.Entities.Analytics;
using Domain.Entities.AssetRegistry;
using Domain.Entities.Configuration;
using Domain.Entities.IoT;
using Domain.Entities.Maintenance;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Domain.Entities.Planning;
using Domain.Entities.Production;
using Domain.Entities.Scada;
using Domain.Entities.Security;

namespace Application.Abstractions.Services;


public interface IAiInsightService : ICrudService<AiInsight>
{
}
public interface IAlarmEventService : ICrudService<AlarmEvent>
{
}
public interface IAlarmRuleService : ICrudService<AlarmRule>
{
}
public interface IAlarmRuleEmailTargetService : ICrudService<AlarmRuleEmailTarget>
{
}
public interface IAlarmRuleSmsTargetService : ICrudService<AlarmRuleSmsTarget>
{
}
public interface IAssetService : ICrudService<Asset>
{
}
public interface IAuditLogService : ICrudService<AuditLog>
{
}
public interface IBatchService : ICrudService<Batch>
{
}
public interface IBatchMaterialService : ICrudService<BatchMaterial>
{
}
public interface IBatchStageLogService : ICrudService<BatchStageLog>
{
}
public interface IBatchUidSequenceService : ICrudService<BatchUidSequence>
{
}
public interface IBillOfMaterialService : ICrudService<BillOfMaterial>
{
}
public interface IBreakdownLogService : ICrudService<BreakdownLog>
{
}
public interface ICustomerService : ICrudService<Customer>
{
}
public interface IDeviceService : ICrudService<Device>
{
}
public interface IDeviceTypeService : ICrudService<DeviceType>
{
}
public interface IDeviceTypeTagDefinitionService : ICrudService<DeviceTypeTagDefinition>
{
}
public interface IFactoryService : ICrudService<Factory>
{
}
public interface IMachineService : ICrudService<Machine>
{
}
public interface IMachineConfigService : ICrudService<MachineConfig>
{
}
public interface IMachineStateService : ICrudService<MachineState>
{
}
public interface IMachineStateHistoryService : ICrudService<MachineStateHistory>
{
}
public interface IMaterialService : ICrudService<Material>
{
}
public interface IMetricThresholdService : ICrudService<MetricThreshold>
{
}
public interface IMrpRequirementService : ICrudService<MrpRequirement>
{
}
public interface IOeeSnapshotService : ICrudService<OeeSnapshot>
{
}
public interface IPlatformSettingService : ICrudService<PlatformSetting>
{
}
public interface IPmScheduleService : ICrudService<PmSchedule>
{
}
public interface IProductionLineService : ICrudService<ProductionLine>
{
}
public interface IProductionRouteService : ICrudService<ProductionRoute>
{
}
public interface IProductionScheduleService : ICrudService<ProductionSchedule>
{
}
public interface IRouteStepService : ICrudService<RouteStep>
{
}
public interface IRouteStepParameterService : ICrudService<RouteStepParameter>
{
}
public interface IScaleService : ICrudService<Scale>
{
}
public interface IShiftService : ICrudService<Shift>
{
}
public interface ISiloService : ICrudService<Silo>
{
}
public interface ISkuService : ICrudService<Sku>
{
}
public interface ISkuProcessParameterService : ICrudService<SkuProcessParameter>
{
}
public interface ISparePartService : ICrudService<SparePart>
{
}
public interface ISparePartMachineTypeService : ICrudService<SparePartMachineType>
{
}
public interface ITelemetryAggregateService : ICrudService<TelemetryAggregate>
{
}
public interface ITelemetryRawService : ICrudService<TelemetryRaw>
{
}
public interface ITelemetryTagService : ICrudService<TelemetryTag>
{
}
public interface IWoPartUsedService : ICrudService<WoPartUsed>
{
}
public interface IWorkOrderService : ICrudService<WorkOrder>
{
}
