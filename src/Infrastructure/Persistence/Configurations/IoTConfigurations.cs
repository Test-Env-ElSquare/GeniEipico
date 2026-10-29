using Domain.Entities.IoT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class DeviceTypeConfiguration : IEntityTypeConfiguration<DeviceType>
{
    public void Configure(EntityTypeBuilder<DeviceType> builder)
    {
        builder.ToTable("DeviceTypes", SchemaNames.IoT);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Protocol).HasMaxLength(30);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class DeviceTypeTagDefinitionConfiguration : IEntityTypeConfiguration<DeviceTypeTagDefinition>
{
    public void Configure(EntityTypeBuilder<DeviceTypeTagDefinition> builder)
    {
        builder.ToTable("DeviceTypeTagDefinitions", SchemaNames.IoT);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.TagName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DataType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(20);
        builder.Property(x => x.MinValue).HasPrecision(14, 4);
        builder.Property(x => x.MaxValue).HasPrecision(14, 4);
        builder.HasOne(x => x.DeviceType).WithMany(x => x.TagDefinitions).HasForeignKey(x => x.DeviceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DeviceTypeId, x.TagName }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices", SchemaNames.IoT);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.SerialNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MqttClientId).HasMaxLength(100);
        builder.Property(x => x.MqttTopicPrefix).HasMaxLength(200);
        builder.Property(x => x.ApiKeyHash).HasMaxLength(200);
        builder.Property(x => x.FirmwareVersion).HasMaxLength(40);
        builder.Property(x => x.HardwareModel).HasMaxLength(80);
        builder.Property(x => x.ConnectionStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LastSeenAt).HasColumnType("datetime2");
        builder.Property(x => x.ProvisionedAt).HasColumnType("datetime2");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Line).WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DeviceType).WithMany(x => x.Devices).HasForeignKey(x => x.DeviceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SerialNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.MqttClientId).IsUnique().HasFilter("[MqttClientId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.CustomerId, x.FactoryId, x.LineId });
    }
}

public class TelemetryRawConfiguration : IEntityTypeConfiguration<TelemetryRaw>
{
    public void Configure(EntityTypeBuilder<TelemetryRaw> builder)
    {
        builder.ToTable("TelemetryRaw", SchemaNames.RealTime);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourcePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ReceivedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.RawPayload).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(x => x.ReceivedAt);
        builder.HasIndex(x => new { x.SourcePath, x.ReceivedAt });
    }
}

public class TelemetryTagConfiguration : IEntityTypeConfiguration<TelemetryTag>
{
    public void Configure(EntityTypeBuilder<TelemetryTag> builder)
    {
        builder.ToTable("TelemetryTags", SchemaNames.RealTime);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceId).HasMaxLength(150).IsRequired();
        builder.Property(x => x.TagName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RecordedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.ValueNum).HasPrecision(18, 6);
        builder.Property(x => x.ValueStr).HasMaxLength(500);
        builder.HasIndex(x => new { x.DeviceId, x.TagName, x.RecordedAt }).IsUnique();
    }
}

public class TelemetryAggregateConfiguration : IEntityTypeConfiguration<TelemetryAggregate>
{
    public void Configure(EntityTypeBuilder<TelemetryAggregate> builder)
    {
        builder.ToTable("TelemetryAggregates", SchemaNames.IoT);
        builder.ConfigureAuditableEntity();
        builder.Property(x => x.TagName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Resolution).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Bucket).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.AvgVal).HasPrecision(18, 6);
        builder.Property(x => x.MinVal).HasPrecision(18, 6);
        builder.Property(x => x.MaxVal).HasPrecision(18, 6);
        builder.Property(x => x.SumVal).HasPrecision(18, 6);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.DeviceId, x.TagName, x.Resolution, x.Bucket }).IsUnique();
        builder.HasIndex(x => new { x.DeviceId, x.TagName, x.Resolution, x.Bucket });
    }
}
