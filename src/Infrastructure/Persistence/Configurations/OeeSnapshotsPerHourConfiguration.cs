using Domain.Entities.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class OeeSnapshotsPerHourConfiguration : IEntityTypeConfiguration<OeeSnapshotsPerHour>
{
    public void Configure(EntityTypeBuilder<OeeSnapshotsPerHour> builder)
    {
        builder.ToTable("OeeSnapshotsPerHour", SchemaNames.Analytics);
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
