using HighlandMilkBar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HighlandMilkBar.Infrastructure.Data.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.Property(x => x.ReceiptNumber).IsRequired().HasMaxLength(64);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.TaxAmount).HasPrecision(18, 2);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.CostOfGoodsSold).HasPrecision(18, 2);
        b.Property(x => x.AmountPaid).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasIndex(x => x.ReceiptNumber).IsUnique();
        b.HasIndex(x => new { x.SoldAt, x.Status });

        b.HasOne(x => x.Customer).WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.CashSession).WithMany(x => x.Sales)
            .HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.Items).WithOne(x => x.Sale)
            .HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne(x => x.Sale)
            .HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> b)
    {
        b.Property(x => x.ItemNameSnapshot).IsRequired().HasMaxLength(200);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.UnitCostSnapshot).HasPrecision(18, 4);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);

        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Recipe).WithMany()
            .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Reference).HasMaxLength(120);
    }
}

public sealed class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> b)
    {
        b.Property(x => x.ReturnNumber).IsRequired().HasMaxLength(64);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.ReturnNumber).IsUnique();

        b.HasOne(x => x.Sale).WithMany(x => x.SalesReturns)
            .HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Items).WithOne(x => x.SalesReturn)
            .HasForeignKey(x => x.SalesReturnId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SalesReturnItemConfiguration : IEntityTypeConfiguration<SalesReturnItem>
{
    public void Configure(EntityTypeBuilder<SalesReturnItem> b)
    {
        b.Property(x => x.ItemNameSnapshot).IsRequired().HasMaxLength(200);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);

        b.HasOne(x => x.SaleItem).WithMany(x => x.ReturnItems)
            .HasForeignKey(x => x.SaleItemId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}