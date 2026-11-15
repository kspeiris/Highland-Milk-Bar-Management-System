using HighlandMilkBar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HighlandMilkBar.Infrastructure.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Sku).IsRequired().HasMaxLength(64);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Barcode).HasMaxLength(64);
        b.Property(x => x.Unit).HasMaxLength(32);
        b.Property(x => x.CostPrice).HasPrecision(18, 2);
        b.Property(x => x.SellingPrice).HasPrecision(18, 2);
        b.Property(x => x.CurrentStock).HasPrecision(18, 3);
        b.Property(x => x.ReorderLevel).HasPrecision(18, 3);

        b.HasIndex(x => x.Sku).IsUnique();
        b.HasIndex(x => x.Barcode);
        b.HasIndex(x => new { x.CategoryId, x.IsActive });

        b.HasOne(x => x.Category).WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.BaseUnit).WithMany()
            .HasForeignKey(x => x.BaseUnitId).OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.ProductUnits).WithOne(x => x.Product)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> b)
    {
        b.Property(x => x.Name).IsRequired().HasMaxLength(64);
        b.Property(x => x.Symbol).IsRequired().HasMaxLength(16);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class ProductUnitConfiguration : IEntityTypeConfiguration<ProductUnit>
{
    public void Configure(EntityTypeBuilder<ProductUnit> b)
    {
        b.Property(x => x.FactorToBase).HasPrecision(18, 4);
        b.HasIndex(x => new { x.ProductId, x.UnitId }).IsUnique();
        b.HasOne(x => x.Unit).WithMany(x => x.ProductUnits)
            .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(64);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.BusinessRegNo).HasMaxLength(64);
        b.Property(x => x.TaxRegNo).HasMaxLength(64);
        b.HasIndex(x => x.Name);
        // Supplier code is unique (case-insensitive) when provided.
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL AND [Code] <> ''");
    }
}

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(200);
        b.HasIndex(x => x.Phone);
    }
}