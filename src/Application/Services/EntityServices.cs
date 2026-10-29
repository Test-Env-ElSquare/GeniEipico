using Application.Abstractions.Repositories;
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

namespace Application.Services;


public class AiInsightService : CrudService<AiInsight>, IAiInsightService
{
    public AiInsightService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class AlarmEventService : CrudService<AlarmEvent>, IAlarmEventService
{
    public AlarmEventService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class AlarmRuleService : CrudService<AlarmRule>, IAlarmRuleService
{
    public AlarmRuleService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class AlarmRuleEmailTargetService : CrudService<AlarmRuleEmailTarget>, IAlarmRuleEmailTargetService
{
    public AlarmRuleEmailTargetService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class AlarmRuleSmsTargetService : CrudService<AlarmRuleSmsTarget>, IAlarmRuleSmsTargetService
{
    public AlarmRuleSmsTargetService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class AssetService : CrudService<Asset>, IAssetService
{
    public AssetService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class AuditLogService : CrudService<AuditLog>, IAuditLogService
{
    public AuditLogService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class BatchService : CrudService<Batch>, IBatchService
{
    public BatchService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public override async Task<Batch> CreateAsync(Batch entity)
    {
        if (entity.CustomerId == 0)
        {
            var customer = (await UnitOfWork.Customers.GetAllAsync()).FirstOrDefault(x => x.IsActive && !x.IsDeleted);
            entity.CustomerId = customer?.Id ?? 1;
        }

        if (string.IsNullOrWhiteSpace(entity.Uid))
        {
            entity.Uid = await GenerateBatchUidAsync(entity.CustomerId);
        }

        var isManualBatch = string.Equals(entity.SourceType, "manual", StringComparison.OrdinalIgnoreCase);
        if (isManualBatch && (!entity.PlannedQty.HasValue || entity.PlannedQty.Value <= 0))
            throw new InvalidOperationException("A positive planned quantity is required for a manual batch.");

        var created = await base.CreateAsync(entity);

        // SAP batches receive their material lines from SAPOrderMaterials, not from Genie's BOM.
        if (!entity.PlannedQty.HasValue)
            return created;

        // Fetch Bill of Materials for this SKU and create BatchMaterial records scaled to planned quantity
        var bom = (await UnitOfWork.BillOfMaterials.GetAllAsync())
            .Where(b => b.SkuId == entity.SkuId)
            .ToList();

        var sku = await UnitOfWork.Skus.GetByIdAsync(entity.SkuId);
        var standardBatchSize = sku?.StandardBatchSize ?? 100000;
        if (standardBatchSize <= 0) standardBatchSize = 100000;
        var scaleFactor = (decimal)entity.PlannedQty.Value / standardBatchSize;

        foreach (var item in bom)
        {
            await UnitOfWork.BatchMaterials.AddAsync(new BatchMaterial
            {
                BatchId = created.Id,
                MaterialId = item.MaterialId,
                PlannedQty = Math.Round(item.QtyPerBatch * scaleFactor, 2),
                DispensedQty = 0m,
                Status = "pending"
            });
        }
        await UnitOfWork.SaveChangesAsync();

        return created;
    }

    private async Task<string> GenerateBatchUidAsync(int customerId)
    {
        var yearMonth = DateTime.UtcNow.ToString("yyMM");
        var sequence = UnitOfWork.BatchUidSequences.Query(asNoTracking: false)
            .FirstOrDefault(x => x.CustomerId == customerId && x.Prefix == "BT" && x.YearMonth == yearMonth);

        if (sequence is null)
        {
            sequence = new BatchUidSequence { CustomerId = customerId, Prefix = "BT", YearMonth = yearMonth, LastSeq = 0 };
            await UnitOfWork.BatchUidSequences.AddAsync(sequence);
        }

        sequence.LastSeq += 1;
        UnitOfWork.BatchUidSequences.Update(sequence);
        await UnitOfWork.SaveChangesAsync();

        var customer = await UnitOfWork.Customers.GetByIdAsync(customerId, asNoTracking: true);
        var country = customer?.CountryCode ?? "EGY";
        return $"BT-{yearMonth}-{sequence.LastSeq:00000}-{country}";
    }
}
public class BatchMaterialService : CrudService<BatchMaterial>, IBatchMaterialService
{
    public BatchMaterialService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class BatchStageLogService : CrudService<BatchStageLog>, IBatchStageLogService
{
    public BatchStageLogService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class BatchUidSequenceService : CrudService<BatchUidSequence>, IBatchUidSequenceService
{
    public BatchUidSequenceService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class BillOfMaterialService : CrudService<BillOfMaterial>, IBillOfMaterialService
{
    public BillOfMaterialService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class BreakdownLogService : CrudService<BreakdownLog>, IBreakdownLogService
{
    public BreakdownLogService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class CustomerService : CrudService<Customer>, ICustomerService
{
    public CustomerService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class DeviceService : CrudService<Device>, IDeviceService
{
    public DeviceService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class DeviceTypeService : CrudService<DeviceType>, IDeviceTypeService
{
    public DeviceTypeService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class DeviceTypeTagDefinitionService : CrudService<DeviceTypeTagDefinition>, IDeviceTypeTagDefinitionService
{
    public DeviceTypeTagDefinitionService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class FactoryService : CrudService<Factory>, IFactoryService
{
    public FactoryService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class MachineService : CrudService<Machine>, IMachineService
{
    public MachineService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class MachineConfigService : CrudService<MachineConfig>, IMachineConfigService
{
    public MachineConfigService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class MachineStateService : CrudService<MachineState>, IMachineStateService
{
    public MachineStateService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class MachineStateHistoryService : CrudService<MachineStateHistory>, IMachineStateHistoryService
{
    public MachineStateHistoryService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class MaterialService : CrudService<Material>, IMaterialService
{
    public MaterialService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class MetricThresholdService : CrudService<MetricThreshold>, IMetricThresholdService
{
    public MetricThresholdService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class MrpRequirementService : CrudService<MrpRequirement>, IMrpRequirementService
{
    public MrpRequirementService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class OeeSnapshotService : CrudService<OeeSnapshot>, IOeeSnapshotService
{
    public OeeSnapshotService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class PlatformSettingService : CrudService<PlatformSetting>, IPlatformSettingService
{
    public PlatformSettingService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class PmScheduleService : CrudService<PmSchedule>, IPmScheduleService
{
    public PmScheduleService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class ProductionLineService : CrudService<ProductionLine>, IProductionLineService
{
    public ProductionLineService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class ProductionRouteService : CrudService<ProductionRoute>, IProductionRouteService
{
    public ProductionRouteService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class ProductionScheduleService : CrudService<ProductionSchedule>, IProductionScheduleService
{
    public ProductionScheduleService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class RouteStepService : CrudService<RouteStep>, IRouteStepService
{
    public RouteStepService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class RouteStepParameterService : CrudService<RouteStepParameter>, IRouteStepParameterService
{
    public RouteStepParameterService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class ScaleService : CrudService<Scale>, IScaleService
{
    public ScaleService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class ShiftService : CrudService<Shift>, IShiftService
{
    public ShiftService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class SiloService : CrudService<Silo>, ISiloService
{
    public SiloService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class SkuService : CrudService<Sku>, ISkuService
{
    public SkuService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class SkuProcessParameterService : CrudService<SkuProcessParameter>, ISkuProcessParameterService
{
    public SkuProcessParameterService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class SparePartService : CrudService<SparePart>, ISparePartService
{
    public SparePartService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class SparePartMachineTypeService : CrudService<SparePartMachineType>, ISparePartMachineTypeService
{
    public SparePartMachineTypeService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class TelemetryAggregateService : CrudService<TelemetryAggregate>, ITelemetryAggregateService
{
    public TelemetryAggregateService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class TelemetryRawService : CrudService<TelemetryRaw>, ITelemetryRawService
{
    public TelemetryRawService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class TelemetryTagService : CrudService<TelemetryTag>, ITelemetryTagService
{
    public TelemetryTagService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class WoPartUsedService : CrudService<WoPartUsed>, IWoPartUsedService
{
    public WoPartUsedService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
public class WorkOrderService : CrudService<WorkOrder>, IWorkOrderService
{
    public WorkOrderService(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }
}
