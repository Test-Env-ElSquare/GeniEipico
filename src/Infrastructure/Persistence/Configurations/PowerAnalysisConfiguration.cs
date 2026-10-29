using Domain.Entities.Analytics;
using Domain.Entities.MasterData;
using Domain.Entities.Organization;
using Domain.Entities.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PowerAnalysisConfiguration : IEntityTypeConfiguration<PowerAnalysis>
{
    public void Configure(EntityTypeBuilder<PowerAnalysis> builder)
    {
        builder.ToTable("PowerAnalysis", SchemaNames.Calculated);
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
        builder.Property(x => x.LastReadingAt).HasColumnType("datetime2(7)");
        builder.Property(x => x.CalculatedAt).HasColumnType("datetime2(7)");
        builder.HasIndex(x => new { x.TransformerId, x.ShiftId, x.ShiftStartTime }).IsUnique();
        builder.HasIndex(x => new { x.CustomerId, x.FactoryId, x.LineId, x.ShiftStartTime });
        builder.HasOne<Transformer>().WithMany().HasForeignKey(x => x.TransformerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Factory>().WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductionLine>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PowerAnalysisViewConfiguration : IEntityTypeConfiguration<PowerAnalysisViewRow>
{
    public void Configure(EntityTypeBuilder<PowerAnalysisViewRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("VW_Batterje_PowerAnalysis", SchemaNames.Calculated);
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
        builder.Property(x => x.LastReadingAt).HasColumnType("datetime2(7)");
        builder.Property(x => x.CalculatedAt).HasColumnType("datetime2(7)");
    }
}
