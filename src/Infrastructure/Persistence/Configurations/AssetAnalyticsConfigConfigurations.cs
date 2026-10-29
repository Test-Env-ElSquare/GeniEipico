using Domain.Entities.Analytics;
using Domain.Entities.AssetRegistry;
using Domain.Entities.Configuration;
using Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets", SchemaNames.Asset);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AssetType).HasMaxLength(60);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.Manufacturer).HasMaxLength(100);
        builder.Property(x => x.InstallDate).HasColumnType("datetime2");
        builder.Property(x => x.WarrantyExpires).HasColumnType("datetime2");
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.UtilizationPct).HasPrecision(5, 2);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.MachineId).IsUnique().HasFilter("[MachineId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => x.DeviceId).IsUnique().HasFilter("[DeviceId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class OeeSnapshotConfiguration : IEntityTypeConfiguration<OeeSnapshot>
{
    public void Configure(EntityTypeBuilder<OeeSnapshot> builder)
    {
        builder.ToTable("OeeSnapshots", SchemaNames.Analytics);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.MachineName).HasMaxLength(100);
        builder.Property(x => x.ShiftDate).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.AvailabilityPct).HasPrecision(5, 2);
        builder.Property(x => x.PerformancePct).HasPrecision(5, 2);
        builder.Property(x => x.QualityPct).HasPrecision(5, 2);
        builder.Property(x => x.OeePct).HasPrecision(5, 2);
        builder.Property(x => x.AverageSpeedRpm).HasPrecision(10, 2);
        builder.Property(x => x.LastState).IsRequired(false);
        builder.Property(x => x.EnergyKwh).HasPrecision(10, 3);
        builder.Property(x => x.ComputedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MachineId, x.ShiftId, x.ShiftDate }).IsUnique();
        builder.HasIndex(x => new { x.MachineId, x.ShiftDate });
    }
}

public class EnergyReadingConfiguration : IEntityTypeConfiguration<EnergyReading>
{
    public void Configure(EntityTypeBuilder<EnergyReading> builder)
    {
        builder.ToTable("EnergyReadings", SchemaNames.Analytics);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Timestamp).HasColumnType("datetime2").IsRequired();

        builder.Property(x => x.VoltageL1).HasPrecision(18, 6);
        builder.Property(x => x.VoltageL2).HasPrecision(18, 6);
        builder.Property(x => x.VoltageL3).HasPrecision(18, 6);
        builder.Property(x => x.CurrentL1).HasPrecision(18, 6);
        builder.Property(x => x.CurrentL2).HasPrecision(18, 6);
        builder.Property(x => x.CurrentL3).HasPrecision(18, 6);
        builder.Property(x => x.PowerFactorL1).HasPrecision(18, 6);
        builder.Property(x => x.PowerFactorL2).HasPrecision(18, 6);
        builder.Property(x => x.PowerFactorL3).HasPrecision(18, 6);
        builder.Property(x => x.ActivePowerL1).HasPrecision(18, 6);
        builder.Property(x => x.ActivePowerL2).HasPrecision(18, 6);
        builder.Property(x => x.ActivePowerL3).HasPrecision(18, 6);
        builder.Property(x => x.ReactivePowerL1).HasPrecision(18, 6);
        builder.Property(x => x.ReactivePowerL2).HasPrecision(18, 6);
        builder.Property(x => x.ReactivePowerL3).HasPrecision(18, 6);
        builder.Property(x => x.ApparentPowerL1).HasPrecision(18, 6);
        builder.Property(x => x.ApparentPowerL2).HasPrecision(18, 6);
        builder.Property(x => x.ApparentPowerL3).HasPrecision(18, 6);
        builder.Property(x => x.EnergyL1).HasPrecision(18, 6);
        builder.Property(x => x.EnergyL2).HasPrecision(18, 6);
        builder.Property(x => x.EnergyL3).HasPrecision(18, 6);
        builder.Property(x => x.VoltageThdL1).HasPrecision(18, 6);
        builder.Property(x => x.VoltageThdL2).HasPrecision(18, 6);
        builder.Property(x => x.VoltageThdL3).HasPrecision(18, 6);
        builder.Property(x => x.CurrentThdL1).HasPrecision(18, 6);
        builder.Property(x => x.CurrentThdL2).HasPrecision(18, 6);
        builder.Property(x => x.CurrentThdL3).HasPrecision(18, 6);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Factory)
            .WithMany()
            .HasForeignKey(x => x.FactoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Line)
            .WithMany()
            .HasForeignKey(x => x.LineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Transformer)
            .WithMany()
            .HasForeignKey(x => x.TransformerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TransformerId, x.Timestamp }).IsUnique();
        builder.HasIndex(x => x.SourceRecordId).IsUnique().HasFilter("[SourceRecordId] IS NOT NULL");
        builder.HasIndex(x => new { x.CustomerId, x.FactoryId, x.LineId, x.Timestamp });
        builder.HasIndex(x => x.FactoryId);
        builder.HasIndex(x => x.LineId);
    }
}

public class EnergyHarmonicReadingConfiguration : IEntityTypeConfiguration<EnergyHarmonicReading>
{
    public void Configure(EntityTypeBuilder<EnergyHarmonicReading> builder)
    {
        builder.ToTable("EnergyHarmonicReadings", SchemaNames.Analytics);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Timestamp).HasColumnType("datetime2").IsRequired();

        foreach (var property in typeof(EnergyHarmonicReading).GetProperties()
                     .Where(x => x.PropertyType == typeof(decimal)))
        {
            builder.Property(property.Name).HasPrecision(18, 3);
        }

        builder.HasOne(x => x.Transformer)
            .WithMany()
            .HasForeignKey(x => x.TransformerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TransformerId, x.Timestamp });
        builder.HasIndex(x => x.SourceRecordId).IsUnique().HasFilter("[SourceRecordId] IS NOT NULL");
    }
}

public class AiInsightConfiguration : IEntityTypeConfiguration<AiInsight>
{
    public void Configure(EntityTypeBuilder<AiInsight> builder)
    {
        builder.ToTable("AiInsights", SchemaNames.Analytics);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.InsightType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.ImpactValue).HasPrecision(14, 2);
        builder.Property(x => x.ImpactCurrency).HasMaxLength(3);
        builder.Property(x => x.ConfidencePct).HasPrecision(5, 2);
        builder.Property(x => x.ModelVersion).HasMaxLength(30);
        builder.Property(x => x.GeneratedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.ExpiresAt).HasColumnType("datetime2");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.EntityType, x.EntityId });
    }
}

public class MachineConfigConfiguration : IEntityTypeConfiguration<MachineConfig>
{
    public void Configure(EntityTypeBuilder<MachineConfig> builder)
    {
        builder.ToTable("MachineConfigs", SchemaNames.Configuration);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.MaxTempCelsius).HasPrecision(6, 2);
        builder.Property(x => x.MaxVibrationMms).HasPrecision(6, 3);
        builder.Property(x => x.RatedPowerKw).HasPrecision(8, 2);
        builder.HasOne(x => x.Machine).WithOne().HasForeignKey<MachineConfig>(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.MachineId).IsUnique();
    }
}

public class MetricThresholdConfiguration : IEntityTypeConfiguration<MetricThreshold>
{
    public void Configure(EntityTypeBuilder<MetricThreshold> builder)
    {
        builder.ToTable("MetricThresholds", SchemaNames.Configuration);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Metric).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Grade).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MinValue).HasPrecision(8, 3);
        builder.Property(x => x.MaxValue).HasPrecision(8, 3);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.Metric, x.Grade }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> builder)
    {
        builder.ToTable("PlatformSettings", SchemaNames.Configuration);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.Key).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Value).HasColumnType("nvarchar(max)");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.Key }).IsUnique();
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", SchemaNames.Security);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.Action).HasMaxLength(60).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(40);
        builder.Property(x => x.OldValues).HasColumnType("nvarchar(max)");
        builder.Property(x => x.NewValues).HasColumnType("nvarchar(max)");
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.OccurredAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.OccurredAt });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
