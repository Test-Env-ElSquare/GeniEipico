using Domain.Entities.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class EipicoOeeViewConfiguration : IEntityTypeConfiguration<EipicoOeeViewRow>
{
    public void Configure(EntityTypeBuilder<EipicoOeeViewRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("VW_EipicoOee", SchemaNames.Analytics);
        ConfigureCommon(builder);
    }

    internal static void ConfigureCommon<T>(EntityTypeBuilder<T> builder)
        where T : class
    {
        builder.Property("MachineName").HasMaxLength(150).IsRequired();
        builder.Property("ShiftName").HasMaxLength(100).IsRequired();
        builder.Property("ShiftStartTime").HasColumnType("datetime2(7)");
        builder.Property("ShiftEndTime").HasColumnType("datetime2(7)");
        builder.Property("Count").HasPrecision(38, 6);
        builder.Property("Qreject").HasPrecision(38, 6);
        builder.Property("Availability").HasPrecision(7, 4);
        builder.Property("AVGSpeed").HasPrecision(20, 2);
        builder.Property("Quality").HasPrecision(18, 6);
        builder.Property("Performance").HasPrecision(20, 2);
        builder.Property("OEE").HasPrecision(20, 4);
    }
}

public sealed class EipicoOeePerHourViewConfiguration : IEntityTypeConfiguration<EipicoOeePerHourViewRow>
{
    public void Configure(EntityTypeBuilder<EipicoOeePerHourViewRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("VW_EipicoOee_PerHour", SchemaNames.Analytics);
        EipicoOeeViewConfiguration.ConfigureCommon(builder);
        builder.Property(x => x.HourStartTime).HasColumnType("datetime2(7)");
        builder.Property(x => x.HourEndTime).HasColumnType("datetime2(7)");
    }
}
