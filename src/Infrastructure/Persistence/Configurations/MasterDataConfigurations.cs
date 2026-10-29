using Domain.Entities.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SkuConfiguration : IEntityTypeConfiguration<Sku>
{
    public void Configure(EntityTypeBuilder<Sku> builder)
    {
        builder.ToTable("Skus", SchemaNames.MasterData);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80);
        builder.Property(x => x.Uom).HasMaxLength(20).IsRequired();
        builder.Property(x => x.UnitWeightG).HasPrecision(8, 4);
        builder.Property(x => x.ErpMaterialCode).HasMaxLength(60);
        builder.Property(x => x.LastErpSyncAt).HasColumnType("datetime2");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SkuProcessParameterConfiguration : IEntityTypeConfiguration<SkuProcessParameter>
{
    public void Configure(EntityTypeBuilder<SkuProcessParameter> builder)
    {
        builder.ToTable("SkuProcessParameters", SchemaNames.MasterData);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(20);
        builder.Property(x => x.DataType).HasMaxLength(20).IsRequired();
        builder.HasOne(x => x.Sku).WithMany(x => x.ProcessParameters).HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SkuId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("Materials", SchemaNames.MasterData);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MaterialType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Uom).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ErpCode).HasMaxLength(60);
        builder.Property(x => x.SupplierCode).HasMaxLength(60);
        builder.Property(x => x.ReorderPointKg).HasPrecision(10, 3);
        builder.Property(x => x.MinStockKg).HasPrecision(10, 3);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class BillOfMaterialConfiguration : IEntityTypeConfiguration<BillOfMaterial>
{
    public void Configure(EntityTypeBuilder<BillOfMaterial> builder)
    {
        builder.ToTable("BillOfMaterials", SchemaNames.MasterData);
        builder.ConfigureSoftDeleteEntity();
        builder.Property(x => x.QtyPerBatch).HasPrecision(12, 4);
        builder.Property(x => x.TolerancePct).HasPrecision(5, 2).HasDefaultValue(2m);
        builder.HasOne(x => x.Sku).WithMany(x => x.BillOfMaterials).HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Material).WithMany(x => x.BillOfMaterials).HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SkuId, x.MaterialId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class TransformerConfiguration : IEntityTypeConfiguration<Transformer>
{
    public void Configure(EntityTypeBuilder<Transformer> builder)
    {
        builder.ToTable("Transformers", SchemaNames.MasterData);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.TransformerName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.HasOne(x => x.Factory)
            .WithMany()
            .HasForeignKey(x => x.FactoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.FactoryId);
        builder.HasIndex(x => x.TransformerName)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
