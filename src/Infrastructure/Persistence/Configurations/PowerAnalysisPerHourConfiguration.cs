using Domain.Entities.Analytics;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Domain.Entities.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PowerAnalysisPerHourConfiguration : IEntityTypeConfiguration<PowerAnalysisPerHour>
{
    public void Configure(EntityTypeBuilder<PowerAnalysisPerHour> builder)
    {
        builder.ToTable("PowerAnalysisPerHour", SchemaNames.Calculated);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.TransformerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ShiftName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(7).IsUnicode(false).IsRequired();
        builder.Property(x => x.TotalEnergyConsumption).HasPrecision(38, 6);
        builder.Property(x => x.PowerFactor).HasPrecision(28, 6);
        builder.Property(x => x.Voltage).HasPrecision(28, 6);
        builder.Property(x => x.Current).HasPrecision(28, 6);
        builder.Property(x => x.Hermonics).HasPrecision(28, 6);
        builder.Property(x => x.AvgTHDv).HasPrecision(28, 6);
        builder.Property(x => x.AvgTHDi).HasPrecision(28, 6);
        builder.Property(x => x.ShiftStartTime).HasColumnType("datetime2(7)");
        builder.Property(x => x.ShiftEndTime).HasColumnType("datetime2(7)");
        builder.Property(x => x.HourStartTime).HasColumnType("datetime2(7)");
        builder.Property(x => x.HourEndTime).HasColumnType("datetime2(7)");
        builder.Property(x => x.LastReadingAt).HasColumnType("datetime2(7)");
        builder.Property(x => x.CalculatedAt).HasColumnType("datetime2(7)");
        builder.HasIndex(x => new { x.TransformerId, x.ShiftId, x.HourStartTime }).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.FactoryId, x.LineId, x.HourStartTime });
        builder.HasOne<Transformer>().WithMany().HasForeignKey(x => x.TransformerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Factory>().WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductionLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PowerAnalysisPerHourViewConfiguration : IEntityTypeConfiguration<PowerAnalysisPerHourViewRow>
{
    public void Configure(EntityTypeBuilder<PowerAnalysisPerHourViewRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("VW_Batterje_PowerAnalysis_PerHour", SchemaNames.Calculated);
        builder.Property(x => x.TransformerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ShiftName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(7).IsUnicode(false).IsRequired();
        builder.Property(x => x.TotalEnergyConsumption).HasPrecision(38, 6);
        builder.Property(x => x.PowerFactor).HasPrecision(28, 6);
        builder.Property(x => x.Voltage).HasPrecision(28, 6);
        builder.Property(x => x.Current).HasPrecision(28, 6);
        builder.Property(x => x.Hermonics).HasPrecision(28, 6);
        builder.Property(x => x.AvgTHDv).HasPrecision(28, 6);
        builder.Property(x => x.AvgTHDi).HasPrecision(28, 6);
        builder.Property(x => x.ShiftStartTime).HasColumnType("datetime2(7)");
        builder.Property(x => x.ShiftEndTime).HasColumnType("datetime2(7)");
        builder.Property(x => x.HourStartTime).HasColumnType("datetime2(7)");
        builder.Property(x => x.HourEndTime).HasColumnType("datetime2(7)");
        builder.Property(x => x.LastReadingAt).HasColumnType("datetime2(7)");
        builder.Property(x => x.CalculatedAt).HasColumnType("datetime2(7)");
    }
}
