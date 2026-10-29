using Domain.Entities.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SyncCheckpointConfiguration : IEntityTypeConfiguration<SyncCheckpoint>
{
    public void Configure(EntityTypeBuilder<SyncCheckpoint> builder)
    {
        builder.ToTable("SyncCheckpoints", SchemaNames.Integration);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceSystem).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StreamName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        builder.HasIndex(x => new { x.SourceSystem, x.StreamName }).IsUnique();
    }
}

public class SyncErrorConfiguration : IEntityTypeConfiguration<SyncError>
{
    public void Configure(EntityTypeBuilder<SyncError> builder)
    {
        builder.ToTable("SyncErrors", SchemaNames.Integration);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceSystem).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StreamName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.FirstOccurredAt).HasColumnType("datetime2");
        builder.Property(x => x.LastOccurredAt).HasColumnType("datetime2");
        builder.Property(x => x.ResolvedAt).HasColumnType("datetime2");
        builder.HasIndex(x => new { x.SourceSystem, x.StreamName, x.SourceRecordId });
        builder.HasIndex(x => x.ResolvedAt);
    }
}
