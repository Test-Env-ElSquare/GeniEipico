using Domain.Entities.Maintenance;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders", SchemaNames.Maintenance);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Number).HasMaxLength(20).IsRequired();
        builder.Property(x => x.WorkOrderType).HasConversion(new EnumStringValueConverter<WorkOrderType>()).HasMaxLength(15).IsRequired();
        builder.Property(x => x.Priority).HasConversion(new EnumStringValueConverter<WorkOrderPriority>()).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Description).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Team).HasMaxLength(60);
        builder.Property(x => x.PlannedStart).HasColumnType("datetime2");
        builder.Property(x => x.PlannedDurationHours).HasPrecision(6, 2);
        builder.Property(x => x.ActualStart).HasColumnType("datetime2");
        builder.Property(x => x.ActualEnd).HasColumnType("datetime2");
        builder.Property(x => x.Status).HasConversion(new EnumStringValueConverter<WorkOrderStatus>()).HasMaxLength(15).IsRequired();
        builder.Property(x => x.EstimatedCost).HasPrecision(12, 2);
        builder.Property(x => x.ActualCost).HasPrecision(12, 2);
        builder.Property(x => x.FailureMode).HasMaxLength(100);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Scale).WithMany().HasForeignKey(x => x.ScaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PmSchedule).WithMany().HasForeignKey(x => x.PmScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AlarmEvent).WithMany().HasForeignKey(x => x.AlarmEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Number).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.CustomerId, x.Status, x.PlannedStart });
    }
}

public class WoPartUsedConfiguration : IEntityTypeConfiguration<WoPartUsed>
{
    public void Configure(EntityTypeBuilder<WoPartUsed> builder)
    {
        builder.ToTable("WoPartsUsed", SchemaNames.Maintenance);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.QtyUsed).HasPrecision(10, 3);
        builder.Property(x => x.UnitCostAtUse).HasPrecision(10, 2);
        builder.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SparePart).WithMany().HasForeignKey(x => x.SparePartId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PmScheduleConfiguration : IEntityTypeConfiguration<PmSchedule>
{
    public void Configure(EntityTypeBuilder<PmSchedule> builder)
    {
        builder.ToTable("PmSchedules", SchemaNames.Maintenance);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.TaskName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Frequency).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LastDoneAt).HasColumnType("datetime2");
        builder.Property(x => x.NextDueAt).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.AssignedTeam).HasMaxLength(60);
        builder.Property(x => x.EstimatedDurationHours).HasPrecision(5, 2);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.NextDueAt, x.IsActive });
    }
}

public class BreakdownLogConfiguration : IEntityTypeConfiguration<BreakdownLog>
{
    public void Configure(EntityTypeBuilder<BreakdownLog> builder)
    {
        builder.ToTable("BreakdownLogs", SchemaNames.Maintenance);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.Reference).HasMaxLength(20);
        builder.Property(x => x.BreakdownAt).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.ResolvedAt).HasColumnType("datetime2");
        builder.Property(x => x.MttrHours).HasPrecision(6, 2);
        builder.Property(x => x.FailureMode).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(15).HasDefaultValue("open");
        builder.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AlarmEvent).WithMany().HasForeignKey(x => x.AlarmEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Reference).IsUnique().HasFilter("[Reference] IS NOT NULL");
    }
}

public class SparePartConfiguration : IEntityTypeConfiguration<SparePart>
{
    public void Configure(EntityTypeBuilder<SparePart> builder)
    {
        builder.ToTable("SpareParts", SchemaNames.Maintenance);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(60);
        builder.Property(x => x.Uom).HasMaxLength(20).IsRequired();
        builder.Property(x => x.QtyOnHand).HasPrecision(10, 3).HasDefaultValue(0m);
        builder.Property(x => x.MinStock).HasPrecision(10, 3);
        builder.Property(x => x.UnitCost).HasPrecision(10, 2);
        builder.Property(x => x.SupplierName).HasMaxLength(150);
        builder.Property(x => x.StockStatus).HasMaxLength(10);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SparePartMachineTypeConfiguration : IEntityTypeConfiguration<SparePartMachineType>
{
    public void Configure(EntityTypeBuilder<SparePartMachineType> builder)
    {
        builder.ToTable("SparePartMachineTypes", SchemaNames.Maintenance);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.MachineType).HasMaxLength(60).IsRequired();
        builder.HasOne(x => x.SparePart).WithMany(x => x.ApplicableMachineTypes).HasForeignKey(x => x.SparePartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SparePartId, x.MachineType }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
