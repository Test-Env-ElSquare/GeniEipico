using Application.Abstractions.Repositories;
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
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Repository;

public class UnitOfWork : IUnitOfWork
{
    private readonly MesDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _currentTransaction;

    public UnitOfWork(MesDbContext context)
    {
        _context = context;
    }


    public IRepository<TEntity> Repository<TEntity>() where TEntity : Entity
    {
        var entityType = typeof(TEntity);

        if (_repositories.TryGetValue(entityType, out var repository))
        {
            return (IRepository<TEntity>)repository;
        }

        var newRepository = new Repository<TEntity>(_context);
        _repositories.Add(entityType, newRepository);
        return newRepository;
    }

    public IRepository<AiInsight> AiInsights => Repository<AiInsight>();
    public IRepository<AlarmEvent> AlarmEvents => Repository<AlarmEvent>();
    public IRepository<AlarmRule> AlarmRules => Repository<AlarmRule>();
    public IRepository<AlarmRuleAction> AlarmRuleActions => Repository<AlarmRuleAction>();
    public IRepository<AlarmRuleEmailTarget> AlarmRuleEmailTargets => Repository<AlarmRuleEmailTarget>();
    public IRepository<AlarmRuleScope> AlarmRuleScopes => Repository<AlarmRuleScope>();
    public IRepository<AlarmRuleSmsTarget> AlarmRuleSmsTargets => Repository<AlarmRuleSmsTarget>();
    public IRepository<Asset> Assets => Repository<Asset>();
    public IRepository<AuditLog> AuditLogs => Repository<AuditLog>();
    public IRepository<Batch> Batches => Repository<Batch>();
    public IRepository<BatchMaterial> BatchMaterials => Repository<BatchMaterial>();
    public IRepository<BatchStageLog> BatchStageLogs => Repository<BatchStageLog>();
    public IRepository<BatchUidSequence> BatchUidSequences => Repository<BatchUidSequence>();
    public IRepository<WeighingEvent> WeighingEvents => Repository<WeighingEvent>();
    public IRepository<MaterialSplit> MaterialSplits => Repository<MaterialSplit>();
    public IRepository<MachineMaterialLoad> MachineMaterialLoads => Repository<MachineMaterialLoad>();
    public IRepository<BillOfMaterial> BillOfMaterials => Repository<BillOfMaterial>();
    public IRepository<BreakdownLog> BreakdownLogs => Repository<BreakdownLog>();
    public IRepository<Customer> Customers => Repository<Customer>();
    public IRepository<Device> Devices => Repository<Device>();
    public IRepository<DeviceType> DeviceTypes => Repository<DeviceType>();
    public IRepository<DeviceTypeTagDefinition> DeviceTypeTagDefinitions => Repository<DeviceTypeTagDefinition>();
    public IRepository<EnergyReading> EnergyReadings => Repository<EnergyReading>();
    public IRepository<EnergyHarmonicReading> EnergyHarmonicReadings => Repository<EnergyHarmonicReading>();
    public IRepository<PowerAnalysis> PowerAnalyses => Repository<PowerAnalysis>();
    public IRepository<PowerAnalysisPerHour> PowerAnalysesPerHour => Repository<PowerAnalysisPerHour>();
    public IRepository<Factory> Factories => Repository<Factory>();
    public IRepository<Machine> Machines => Repository<Machine>();
    public IRepository<MachineConfig> MachineConfigs => Repository<MachineConfig>();
    public IRepository<MachineState> MachineStates => Repository<MachineState>();
    public IRepository<MachineStateHistory> MachineStateHistories => Repository<MachineStateHistory>();
    public IRepository<Material> Materials => Repository<Material>();
    public IRepository<MetricThreshold> MetricThresholds => Repository<MetricThreshold>();
    public IRepository<MrpRequirement> MrpRequirements => Repository<MrpRequirement>();
    public IRepository<OeeSnapshot> OeeSnapshots => Repository<OeeSnapshot>();
    public IRepository<PlatformSetting> PlatformSettings => Repository<PlatformSetting>();
    public IRepository<PmSchedule> PmSchedules => Repository<PmSchedule>();
    public IRepository<ProductionLine> ProductionLines => Repository<ProductionLine>();
    public IRepository<ProductionRoute> ProductionRoutes => Repository<ProductionRoute>();
    public IRepository<ProductionSchedule> ProductionSchedules => Repository<ProductionSchedule>();
    public IRepository<RouteStep> RouteSteps => Repository<RouteStep>();
    public IRepository<RouteStepParameter> RouteStepParameters => Repository<RouteStepParameter>();
    public IRepository<Scale> Scales => Repository<Scale>();
    public IRepository<Shift> Shifts => Repository<Shift>();
    public IRepository<ShiftFactory> ShiftFactories => Repository<ShiftFactory>();
    public IRepository<Silo> Silos => Repository<Silo>();
    public IRepository<Sku> Skus => Repository<Sku>();
    public IRepository<SkuProcessParameter> SkuProcessParameters => Repository<SkuProcessParameter>();
    public IRepository<SparePart> SpareParts => Repository<SparePart>();
    public IRepository<SparePartMachineType> SparePartMachineTypes => Repository<SparePartMachineType>();
    public IRepository<TelemetryAggregate> TelemetryAggregates => Repository<TelemetryAggregate>();
    public IRepository<TelemetryRaw> TelemetryRaw => Repository<TelemetryRaw>();
    public IRepository<TelemetryTag> TelemetryTags => Repository<TelemetryTag>();
    public IRepository<Transformer> Transformers => Repository<Transformer>();
    public IRepository<WoPartUsed> WoPartsUsed => Repository<WoPartUsed>();
    public IRepository<WorkOrder> WorkOrders => Repository<WorkOrder>();
    public IRepository<SyncCheckpoint> SyncCheckpoints => Repository<SyncCheckpoint>();
    public IRepository<SyncError> SyncErrors => Repository<SyncError>();

    public Task<int> SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            return;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_currentTransaction is null)
        {
            await SaveChangesAsync();
            return;
        }

        try
        {
            await SaveChangesAsync();
            await _currentTransaction.CommitAsync();
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_currentTransaction is null)
        {
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync();
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await operation();
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
        }

        await _context.DisposeAsync();
    }
}
