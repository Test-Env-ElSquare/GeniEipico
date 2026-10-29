using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", SchemaNames.Organization);
        builder.ConfigureSoftDeleteEntity();

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Tier).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SubscriptionStarts).HasColumnType("datetime2");
        builder.Property(x => x.SubscriptionEnds).HasColumnType("datetime2");
        builder.Property(x => x.CountryCode).HasMaxLength(3);
        builder.Property(x => x.ContactEmail).HasMaxLength(200);
        builder.Property(x => x.Industry).HasMaxLength(80);
        builder.Property(x => x.ErpSystem).HasMaxLength(60);
        builder.Property(x => x.ErpEndpointUrl).HasMaxLength(500);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class FactoryConfiguration : IEntityTypeConfiguration<Factory>
{
    public void Configure(EntityTypeBuilder<Factory> builder)
    {
        builder.ToTable("Factories", SchemaNames.Organization);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.City).HasMaxLength(80);
        builder.Property(x => x.CountryCode).HasMaxLength(3);
        builder.Property(x => x.TimeZone).HasMaxLength(60).IsRequired();
        builder.HasOne(x => x.Customer).WithMany(x => x.Factories).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ProductionLineConfiguration : IEntityTypeConfiguration<ProductionLine>
{
    public void Configure(EntityTypeBuilder<ProductionLine> builder)
    {
        builder.ToTable("ProductionLines", SchemaNames.Organization);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Area).HasMaxLength(100);
        builder.Property(x => x.ViewName).HasMaxLength(100);
        builder.Property(x => x.AliasName).HasMaxLength(200);
        builder.Property(x => x.ProductType).HasMaxLength(60);
        builder.HasOne(x => x.Factory).WithMany(x => x.ProductionLines).HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FactoryId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ProductionLineTransformerConfiguration : IEntityTypeConfiguration<ProductionLineTransformer>
{
    public void Configure(EntityTypeBuilder<ProductionLineTransformer> builder)
    {
        builder.ToTable("ProductionLineTransformers", SchemaNames.Organization);
        builder.HasKey(x => new { x.ProductionLineId, x.TransformerId });
        builder.HasOne(x => x.ProductionLine)
            .WithMany(x => x.TransformerLinks)
            .HasForeignKey(x => x.ProductionLineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Transformer)
            .WithMany(x => x.LineLinks)
            .HasForeignKey(x => x.TransformerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TransformerId);
    }
}
