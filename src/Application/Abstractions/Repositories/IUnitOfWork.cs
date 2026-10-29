using Domain.Common;
using Domain.Entities.Alarms;
using Domain.Entities.Analytics;
using Domain.Entities.AssetRegistry;
using Domain.Entities.Configuration;
using Domain.Entities.IoT;
using Domain.Entities.Integration;
using Domain.Entities.Maintenance;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Domain.Entities.Planning;
using Domain.Entities.Production;
using Domain.Entities.Scada;
using Domain.Entities.Security;

namespace Application.Abstractions.Repositories;

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : Entity;

    IRepository<AiInsight> AiInsights { get; }
    IRepository<AlarmEvent> AlarmEvents { get; }
    IRepository<AlarmRule> AlarmRules { get; }
    IRepository<AlarmRuleAction> AlarmRuleActions { get; }
    IRepository<AlarmRuleEmailTarget> AlarmRuleEmailTargets { get; }
    IRepository<AlarmRuleScope> AlarmRuleScopes { get; }
    IRepository<AlarmRuleSmsTarget> AlarmRuleSmsTargets { get; }
    IRepository<Asset> Assets { get; }
    IRepository<AuditLog> AuditLogs { get; }
    IRepository<Batch> Batches { get; }
    IRepository<BatchMaterial> BatchMaterials { get; }
    IRepository<BatchStageLog> BatchStageLogs { get; }
    IRepository<BatchUidSequence> BatchUidSequences { get; }
    IRepository<WeighingEvent> WeighingEvents { get; }
    IRepository<MaterialSplit> MaterialSplits { get; }
    IRepository<MachineMaterialLoad> MachineMaterialLoads { get; }
    IRepository<BillOfMaterial> BillOfMaterials { get; }
    IRepository<BreakdownLog> BreakdownLogs { get; }
    IRepository<Customer> Customers { get; }
    IRepository<Device> Devices { get; }
    IRepository<DeviceType> DeviceTypes { get; }
    IRepository<DeviceTypeTagDefinition> DeviceTypeTagDefinitions { get; }
    IRepository<EnergyReading> EnergyReadings { get; }
    IRepository<EnergyHarmonicReading> EnergyHarmonicReadings { get; }
    IRepository<PowerAnalysis> PowerAnalyses { get; }
    IRepository<PowerAnalysisPerHour> PowerAnalysesPerHour { get; }
    IRepository<Factory> Factories { get; }
    IRepository<Machine> Machines { get; }
    IRepository<MachineConfig> MachineConfigs { get; }
    IRepository<MachineState> MachineStates { get; }
    IRepository<MachineStateHistory> MachineStateHistories { get; }
    IRepository<Material> Materials { get; }
    IRepository<MetricThreshold> MetricThresholds { get; }
    IRepository<MrpRequirement> MrpRequirements { get; }
    IRepository<OeeSnapshot> OeeSnapshots { get; }
    IRepository<PlatformSetting> PlatformSettings { get; }
    IRepository<PmSchedule> PmSchedules { get; }
    IRepository<ProductionLine> ProductionLines { get; }
    IRepository<ProductionRoute> ProductionRoutes { get; }
    IRepository<ProductionSchedule> ProductionSchedules { get; }
    IRepository<RouteStep> RouteSteps { get; }
    IRepository<RouteStepParameter> RouteStepParameters { get; }
    IRepository<Scale> Scales { get; }
    IRepository<Shift> Shifts { get; }
    IRepository<ShiftFactory> ShiftFactories { get; }
    IRepository<Silo> Silos { get; }
    IRepository<Sku> Skus { get; }
    IRepository<SkuProcessParameter> SkuProcessParameters { get; }
    IRepository<SparePart> SpareParts { get; }
    IRepository<SparePartMachineType> SparePartMachineTypes { get; }
    IRepository<TelemetryAggregate> TelemetryAggregates { get; }
    IRepository<TelemetryRaw> TelemetryRaw { get; }
    IRepository<TelemetryTag> TelemetryTags { get; }
    IRepository<Transformer> Transformers { get; }
    IRepository<WoPartUsed> WoPartsUsed { get; }
    IRepository<WorkOrder> WorkOrders { get; }
    IRepository<SyncCheckpoint> SyncCheckpoints { get; }
    IRepository<SyncError> SyncErrors { get; }

    Task<int> SaveChangesAsync();

    Task BeginTransactionAsync();

    Task CommitTransactionAsync();

    Task RollbackTransactionAsync();

    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation);
}
