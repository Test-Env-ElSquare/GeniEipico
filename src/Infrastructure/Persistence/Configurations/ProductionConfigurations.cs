using Domain.Entities.Production;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class BatchConfiguration : IEntityTypeConfiguration<Batch>
{
    public void Configure(EntityTypeBuilder<Batch> builder)
    {
        builder.ToTable("Batches", SchemaNames.Production);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Uid).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SourceType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.JobOrderId).HasMaxLength(100);
        builder.Property(x => x.WarehouseCode).HasMaxLength(50);
        builder.Property(x => x.StorageLocation).HasMaxLength(50);
        builder.Property(x => x.ReceivedAt).HasColumnType("datetime2");
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Stage).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PlannedStart).HasColumnType("datetime2");
        builder.Property(x => x.PlannedEnd).HasColumnType("datetime2");
        builder.Property(x => x.ActualStart).HasColumnType("datetime2");
        builder.Property(x => x.ActualEnd).HasColumnType("datetime2");
        builder.Property(x => x.Priority)
            .HasConversion(
                value => (short)value,
                value => value == 1
                    ? BatchPriority.Low
                    : value == 3 || value == 10
                        ? BatchPriority.High
                        : BatchPriority.Normal)
            .IsRequired();
        builder.Property(x => x.Notes).HasColumnType("nvarchar(max)");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Sku).WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Line).WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Route).WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ParentBatch).WithMany().HasForeignKey(x => x.ParentBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Uid).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.SourceType, x.SourceRecordId })
            .IsUnique()
            .HasFilter("[SourceRecordId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.CustomerId, x.Status, x.PlannedStart });
    }
}

public class BatchUidSequenceConfiguration : IEntityTypeConfiguration<BatchUidSequence>
{
    public void Configure(EntityTypeBuilder<BatchUidSequence> builder)
    {
        builder.ToTable("BatchUidSequences", SchemaNames.Production);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.Prefix).HasMaxLength(2).IsRequired();
        builder.Property(x => x.YearMonth).HasMaxLength(4).IsRequired();
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.Prefix, x.YearMonth }).IsUnique();
    }
}

public class BatchMaterialConfiguration : IEntityTypeConfiguration<BatchMaterial>
{
    public void Configure(EntityTypeBuilder<BatchMaterial> builder)
    {
        builder.ToTable("BatchMaterials", SchemaNames.Production);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.MaterialUid).HasMaxLength(30);
        builder.Property(x => x.ItemCode).HasMaxLength(100);
        builder.Property(x => x.OperationCode).HasMaxLength(100);
        builder.Property(x => x.Uom).HasMaxLength(30);
        builder.Property(x => x.PlannedQty).HasPrecision(18, 4);
        builder.Property(x => x.DispensedQty).HasPrecision(12, 4).HasDefaultValue(0m);
        builder.Property(x => x.DeviationPct).HasPrecision(6, 3);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.DispenseStartedAt).HasColumnType("datetime2");
        builder.Property(x => x.DispenseCompletedAt).HasColumnType("datetime2");
        builder.HasOne(x => x.Batch).WithMany(x => x.Materials).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Silo).WithMany().HasForeignKey(x => x.SiloId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Scale).WithMany().HasForeignKey(x => x.ScaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.BatchId);
        builder.HasIndex(x => new { x.BatchId, x.MaterialId });
        builder.HasIndex(x => x.SourceRecordId).IsUnique().HasFilter("[SourceRecordId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class WeighingEventConfiguration : IEntityTypeConfiguration<WeighingEvent>
{
    public void Configure(EntityTypeBuilder<WeighingEvent> builder)
    {
        builder.ToTable("WeighingEvents", SchemaNames.Production);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaterialUid).HasMaxLength(30);
        builder.Property(x => x.Weight).HasPrecision(18, 4);
        builder.Property(x => x.RecordedAt).HasColumnType("datetime2");
        builder.Property(x => x.WeighingSource).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.Batch).WithMany(x => x.WeighingEvents).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BatchMaterial).WithMany(x => x.WeighingEvents).HasForeignKey(x => x.BatchMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Scale).WithMany().HasForeignKey(x => x.ScaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BatchId, x.RecordedAt });
        builder.HasIndex(x => new { x.WeighingSource, x.SourceRecordId })
            .IsUnique()
            .HasFilter("[SourceRecordId] IS NOT NULL");
    }
}

public class MaterialSplitConfiguration : IEntityTypeConfiguration<MaterialSplit>
{
    public void Configure(EntityTypeBuilder<MaterialSplit> builder)
    {
        builder.ToTable("MaterialSplits", SchemaNames.Production);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SplitUid).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(18, 4);
        builder.Property(x => x.SplitAt).HasColumnType("datetime2");
        builder.HasOne(x => x.BatchMaterial).WithMany(x => x.MaterialSplits).HasForeignKey(x => x.BatchMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SplitUid).IsUnique();
        builder.HasIndex(x => x.SourceRecordId).IsUnique().HasFilter("[SourceRecordId] IS NOT NULL");
    }
}

public class MachineMaterialLoadConfiguration : IEntityTypeConfiguration<MachineMaterialLoad>
{
    public void Configure(EntityTypeBuilder<MachineMaterialLoad> builder)
    {
        builder.ToTable("MachineMaterialLoads", SchemaNames.Production);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaterialUid).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(18, 4);
        builder.Property(x => x.LoadedAt).HasColumnType("datetime2");
        builder.Property(x => x.ShiftStartTime).HasColumnType("datetime2");
        builder.Property(x => x.ActivationDate).HasColumnType("datetime2");
        builder.Property(x => x.FinishingDate).HasColumnType("datetime2");
        builder.HasOne(x => x.Batch).WithMany(x => x.MachineMaterialLoads).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BatchMaterial).WithMany(x => x.MachineMaterialLoads).HasForeignKey(x => x.BatchMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MaterialSplit).WithMany(x => x.MachineMaterialLoads).HasForeignKey(x => x.MaterialSplitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BatchId, x.MachineId, x.LoadedAt });
        builder.HasIndex(x => x.SourceRecordId).IsUnique().HasFilter("[SourceRecordId] IS NOT NULL");
    }
}

public class BatchStageLogConfiguration : IEntityTypeConfiguration<BatchStageLog>
{
    public void Configure(EntityTypeBuilder<BatchStageLog> builder)
    {
        builder.ToTable("BatchStageLogs", SchemaNames.Production);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.FromStage).HasMaxLength(20);
        builder.Property(x => x.ToStage).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TransitionedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BatchId, x.TransitionedAt });
    }
}
