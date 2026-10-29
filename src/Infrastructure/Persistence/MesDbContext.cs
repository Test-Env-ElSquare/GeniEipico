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
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class MesDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public MesDbContext(DbContextOptions<MesDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Factory> Factories => Set<Factory>();
    public DbSet<ProductionLine> ProductionLines => Set<ProductionLine>();
    public DbSet<ProductionLineTransformer> ProductionLineTransformers => Set<ProductionLineTransformer>();

    public DbSet<DeviceType> DeviceTypes => Set<DeviceType>();
    public DbSet<DeviceTypeTagDefinition> DeviceTypeTagDefinitions => Set<DeviceTypeTagDefinition>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<TelemetryRaw> TelemetryRaw => Set<TelemetryRaw>();
    public DbSet<TelemetryTag> TelemetryTags => Set<TelemetryTag>();
    public DbSet<TelemetryAggregate> TelemetryAggregates => Set<TelemetryAggregate>();

    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MachineState> MachineStates => Set<MachineState>();
    public DbSet<MachineStateHistory> MachineStateHistories => Set<MachineStateHistory>();
    public DbSet<Silo> Silos => Set<Silo>();
    public DbSet<Scale> Scales => Set<Scale>();

    public DbSet<Sku> Skus => Set<Sku>();
    public DbSet<SkuProcessParameter> SkuProcessParameters => Set<SkuProcessParameter>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<BillOfMaterial> BillOfMaterials => Set<BillOfMaterial>();
    public DbSet<Transformer> Transformers => Set<Transformer>();

    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<BatchUidSequence> BatchUidSequences => Set<BatchUidSequence>();
    public DbSet<BatchMaterial> BatchMaterials => Set<BatchMaterial>();
    public DbSet<BatchStageLog> BatchStageLogs => Set<BatchStageLog>();
    public DbSet<WeighingEvent> WeighingEvents => Set<WeighingEvent>();
    public DbSet<MaterialSplit> MaterialSplits => Set<MaterialSplit>();
    public DbSet<MachineMaterialLoad> MachineMaterialLoads => Set<MachineMaterialLoad>();

    public DbSet<ProductionRoute> Routes => Set<ProductionRoute>();
    public DbSet<RouteStep> RouteSteps => Set<RouteStep>();
    public DbSet<RouteStepParameter> RouteStepParameters => Set<RouteStepParameter>();
    public DbSet<ProductionSchedule> ProductionSchedules => Set<ProductionSchedule>();
    public DbSet<MrpRequirement> MrpRequirements => Set<MrpRequirement>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftFactory> ShiftFactories => Set<ShiftFactory>();

    public DbSet<AlarmRule> AlarmRules => Set<AlarmRule>();
    public DbSet<AlarmRuleEmailTarget> AlarmRuleEmailTargets => Set<AlarmRuleEmailTarget>();
    public DbSet<AlarmRuleSmsTarget> AlarmRuleSmsTargets => Set<AlarmRuleSmsTarget>();
    public DbSet<AlarmRuleScope> AlarmRuleScopes => Set<AlarmRuleScope>();
    public DbSet<AlarmRuleAction> AlarmRuleActions => Set<AlarmRuleAction>();
    public DbSet<AlarmEvent> AlarmEvents => Set<AlarmEvent>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WoPartUsed> WoPartsUsed => Set<WoPartUsed>();
    public DbSet<PmSchedule> PmSchedules => Set<PmSchedule>();
    public DbSet<BreakdownLog> BreakdownLogs => Set<BreakdownLog>();
    public DbSet<SparePart> SpareParts => Set<SparePart>();
    public DbSet<SparePartMachineType> SparePartMachineTypes => Set<SparePartMachineType>();

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<EnergyReading> EnergyReadings => Set<EnergyReading>();
    public DbSet<EnergyHarmonicReading> EnergyHarmonicReadings => Set<EnergyHarmonicReading>();
    public DbSet<PowerAnalysis> PowerAnalyses => Set<PowerAnalysis>();
    public DbSet<PowerAnalysisPerHour> PowerAnalysesPerHour => Set<PowerAnalysisPerHour>();
    public DbSet<PowerAnalysisPerHourViewRow> PowerAnalysisPerHourView => Set<PowerAnalysisPerHourViewRow>();
    public DbSet<PowerAnalysisViewRow> PowerAnalysisView => Set<PowerAnalysisViewRow>();
    public DbSet<OeeSnapshot> OeeSnapshots => Set<OeeSnapshot>();
    public DbSet<OeeSnapshotsPerHour> OeeSnapshotsPerHour => Set<OeeSnapshotsPerHour>();
    public DbSet<EipicoOeeViewRow> EipicoOeeView => Set<EipicoOeeViewRow>();
    public DbSet<EipicoOeePerHourViewRow> EipicoOeePerHourView => Set<EipicoOeePerHourViewRow>();
    public DbSet<AiInsight> AiInsights => Set<AiInsight>();

    public DbSet<MachineConfig> MachineConfigs => Set<MachineConfig>();
    public DbSet<MetricThreshold> MetricThresholds => Set<MetricThreshold>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SyncCheckpoint> SyncCheckpoints => Set<SyncCheckpoint>();
    public DbSet<SyncError> SyncErrors => Set<SyncError>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users", SchemaNames.Security);
            entity.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.LastLoginAt).HasColumnType("datetime2");
            entity.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Factory>().WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductionLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.FactoryId);
            entity.HasIndex(x => x.LineId);
        });

        builder.Entity<ApplicationRole>().ToTable("Roles", SchemaNames.Security);
        builder.Entity<IdentityUserRole<int>>().ToTable("UserRoles", SchemaNames.Security);
        builder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims", SchemaNames.Security);
        builder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins", SchemaNames.Security);
        builder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims", SchemaNames.Security);
        builder.Entity<IdentityUserToken<int>>().ToTable("UserTokens", SchemaNames.Security);

        builder.ApplyConfigurationsFromAssembly(typeof(MesDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditValues();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, System.Threading.CancellationToken cancellationToken = default)
    {
        ApplyAuditValues();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditValues()
    {
        // Fail closed if a future write path tries to overwrite a saved signal.
        if (ChangeTracker.Entries<MachineState>().Any(x => x.State == EntityState.Modified))
            throw new InvalidOperationException("RealTime.Production is append-only. Insert a new reading instead of updating an existing record.");

        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<SoftDeleteEntity>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }
        }
    }
}
