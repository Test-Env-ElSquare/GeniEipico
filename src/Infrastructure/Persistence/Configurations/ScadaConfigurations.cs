using Domain.Entities.Scada;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class MachineConfiguration : IEntityTypeConfiguration<Machine>
{
    public void Configure(EntityTypeBuilder<Machine> builder)
    {
        builder.ToTable("Machines", SchemaNames.Scada);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MachineType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Manufacturer).HasMaxLength(100);
        builder.Property(x => x.ModelNumber).HasMaxLength(100);
        builder.Property(x => x.RatedPowerKw).HasPrecision(8, 2);
        builder.Property(x => x.InstallDate).HasColumnType("datetime2");
        builder.HasOne(x => x.Line).WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LineId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.DeviceId).IsUnique().HasFilter("[DeviceId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class MachineStateConfiguration : IEntityTypeConfiguration<MachineState>
{
    public void Configure(EntityTypeBuilder<MachineState> builder)
    {
        builder.ToTable("Production", SchemaNames.RealTime);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MachineId).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CountDiff).HasPrecision(18, 4);
        builder.Property(x => x.Temp).HasPrecision(10, 3);
        builder.Property(x => x.QReject).HasColumnName("Qreject");
        builder.Property(x => x.QRejectDiff).HasColumnName("QrejectDiff").HasPrecision(18, 4);
        builder.Property(x => x.Sku).HasColumnName("SKU");
        builder.Property(x => x.Timestamp).HasColumnName("TimeStamp").HasColumnType("datetime2").IsRequired();
        builder.HasOne<Domain.Entities.Organization.Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Organization.Factory>()
            .WithMany()
            .HasForeignKey(x => x.FactoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Organization.ProductionLine>()
            .WithMany()
            .HasForeignKey(x => x.LineId)
            .OnDelete(DeleteBehavior.Restrict);
        // Production is an append-only signal log, not one mutable row per machine.
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.FactoryId, x.MachineId, x.Timestamp });
        builder.HasIndex(x => new { x.FactoryId, x.LineId });
    }
}

public class MachineStateHistoryConfiguration : IEntityTypeConfiguration<MachineStateHistory>
{
    public void Configure(EntityTypeBuilder<MachineStateHistory> builder)
    {
        builder.ToTable("MachineStateHistories", SchemaNames.Scada);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.FromState).HasMaxLength(20);
        builder.Property(x => x.ToState).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TriggerSource).HasMaxLength(30);
        builder.Property(x => x.TransitionedAt).HasColumnType("datetime2").IsRequired();
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MachineId, x.TransitionedAt });
    }
}

public class SiloConfiguration : IEntityTypeConfiguration<Silo>
{
    public void Configure(EntityTypeBuilder<Silo> builder)
    {
        builder.ToTable("Silos", SchemaNames.RealTime);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SiloType).HasMaxLength(20);
        builder.Property(x => x.Zone).HasMaxLength(30);
        builder.Property(x => x.CapacityKg).HasPrecision(10, 2);
        builder.Property(x => x.LowLevelPct).HasPrecision(5, 2).HasDefaultValue(20m);
        builder.Property(x => x.CriticalLevelPct).HasPrecision(5, 2).HasDefaultValue(10m);
        builder.Property(x => x.CurrentQtyKg).HasPrecision(10, 3);
        builder.Property(x => x.CurrentLotUid).HasMaxLength(30);
        builder.Property(x => x.TelemetryAt).HasColumnType("datetime2");
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurrentMaterial).WithMany().HasForeignKey(x => x.CurrentMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FactoryId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.DeviceId).IsUnique().HasFilter("[DeviceId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class ScaleConfiguration : IEntityTypeConfiguration<Scale>
{
    public void Configure(EntityTypeBuilder<Scale> builder)
    {
        builder.ToTable("Scales", SchemaNames.RealTime);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Zone).HasMaxLength(20);
        builder.Property(x => x.CapacityKg).HasPrecision(8, 3);
        builder.Property(x => x.ResolutionG).HasPrecision(6, 3);
        builder.Property(x => x.Protocol).HasMaxLength(30);
        builder.Property(x => x.TareKg).HasPrecision(8, 3).HasDefaultValue(0m);
        builder.Property(x => x.WeightKg).HasPrecision(10, 4);
        builder.Property(x => x.ScaleState).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CalibratedAt).HasColumnType("datetime2");
        builder.Property(x => x.CalibrationCert).HasMaxLength(200);
        builder.Property(x => x.TelemetryAt).HasColumnType("datetime2");
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActiveBatch).WithMany().HasForeignKey(x => x.ActiveBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActiveMaterial).WithMany().HasForeignKey(x => x.ActiveMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FactoryId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.DeviceId).IsUnique().HasFilter("[DeviceId] IS NOT NULL AND [IsDeleted] = 0");
    }
}
