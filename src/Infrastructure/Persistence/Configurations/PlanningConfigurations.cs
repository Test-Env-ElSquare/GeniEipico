using Domain.Entities.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductionRouteConfiguration : IEntityTypeConfiguration<ProductionRoute>
{
    public void Configure(EntityTypeBuilder<ProductionRoute> builder)
    {
        builder.ToTable("Routes", SchemaNames.Planning);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CycleTimeHours).HasPrecision(6, 2);
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("draft");
        builder.Property(x => x.Version).HasDefaultValue((short)1);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Sku).WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class RouteStepConfiguration : IEntityTypeConfiguration<RouteStep>
{
    public void Configure(EntityTypeBuilder<RouteStep> builder)
    {
        builder.ToTable("RouteSteps", SchemaNames.Planning);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.StepName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.MachineType).HasMaxLength(60);
        builder.HasOne(x => x.Route).WithMany(x => x.Steps).HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.RouteId, x.StepNo }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class RouteStepParameterConfiguration : IEntityTypeConfiguration<RouteStepParameter>
{
    public void Configure(EntityTypeBuilder<RouteStepParameter> builder)
    {
        builder.ToTable("RouteStepParameters", SchemaNames.Planning);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(20);
        builder.Property(x => x.DataType).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.RouteStep).WithMany(x => x.Parameters).HasForeignKey(x => x.RouteStepId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.RouteStepId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ProductionScheduleConfiguration : IEntityTypeConfiguration<ProductionSchedule>
{
    public void Configure(EntityTypeBuilder<ProductionSchedule> builder)
    {
        builder.ToTable("ProductionSchedules", SchemaNames.Planning);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.ScheduledStart).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.ScheduledEnd).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.StoppageType).HasMaxLength(30);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Line).WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LineId, x.ScheduledStart, x.ScheduledEnd });
    }
}

public class MrpRequirementConfiguration : IEntityTypeConfiguration<MrpRequirement>
{
    public void Configure(EntityTypeBuilder<MrpRequirement> builder)
    {
        builder.ToTable("MrpRequirements", SchemaNames.Planning);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.PlanDate).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.RequiredQty).HasPrecision(12, 3);
        builder.Property(x => x.AvailableQty).HasPrecision(12, 3);
        builder.Property(x => x.GapQty).HasPrecision(12, 3);
        builder.Property(x => x.MrpStatus).HasMaxLength(15).IsRequired();
        builder.Property(x => x.PoReference).HasMaxLength(60);
        builder.Property(x => x.PoExpectedDate).HasColumnType("datetime2");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.MaterialId, x.PlanDate }).IsUnique();
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts", SchemaNames.Planning);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.StartTime).HasColumnType("time");
        builder.Property(x => x.EndTime).HasColumnType("time");
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FactoryId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ShiftFactoryConfiguration : IEntityTypeConfiguration<ShiftFactory>
{
    public void Configure(EntityTypeBuilder<ShiftFactory> builder)
    {
        builder.ToTable("ShiftFactories", SchemaNames.Planning);
        builder.ConfigureAuditableEntity();
        builder.HasOne(x => x.Shift).WithMany(x => x.FactoryLinks).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Factory).WithMany(x => x.ShiftLinks).HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ShiftId, x.FactoryId }).IsUnique();
        builder.HasIndex(x => x.FactoryId);
    }
}
