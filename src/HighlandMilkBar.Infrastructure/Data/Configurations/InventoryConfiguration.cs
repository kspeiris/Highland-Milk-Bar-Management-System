using HighlandMilkBar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HighlandMilkBar.Infrastructure.Data.Configurations;

public sealed class InventoryBatchConfiguration : IEntityTypeConfiguration<InventoryBatch>
{
    public void Configure(EntityTypeBuilder<InventoryBatch> b)
    {
        b.Property(x => x.BatchNumber).IsRequired().HasMaxLength(64);
        b.Property(x => x.QuantityReceived).HasPrecision(18, 3);
        b.Property(x => x.QuantityRemaining).HasPrecision(18, 3);
        b.Property(x => x.UnitCost).HasPrecision(18, 4);
        b.HasIndex(x => new { x.ProductId, x.BatchNumber });
        b.HasIndex(x => x.ExpiryDate);

        b.HasOne(x => x.Product).WithMany(x => x.InventoryBatches)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Supplier).WithMany()
            .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.PurchaseItem).WithMany(x => x.InventoryBatches)
            .HasForeignKey(x => x.PurchaseItemId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.Property(x => x.QuantityChange).HasPrecision(18, 3);
        b.Property(x => x.UnitCostSnapshot).HasPrecision(18, 4);
        b.Property(x => x.BalanceAfter).HasPrecision(18, 3);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.ProductId, x.CreatedAt });
        b.HasIndex(x => x.ReferenceType);

        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.InventoryBatch).WithMany()
            .HasForeignKey(x => x.InventoryBatchId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> b)
    {
        b.Property(x => x.ReferenceNumber).IsRequired().HasMaxLength(64);
        b.Property(x => x.Reason).IsRequired().HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.ReferenceNumber).IsUnique();
        b.HasMany(x => x.Items).WithOne(x => x.StockAdjustment)
            .HasForeignKey(x => x.StockAdjustmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StockAdjustmentItemConfiguration : IEntityTypeConfiguration<StockAdjustmentItem>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentItem> b)
    {
        b.Property(x => x.QuantityChange).HasPrecision(18, 3);
        b.Property(x => x.UnitCostSnapshot).HasPrecision(18, 4);
        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.InventoryBatch).WithMany()
            .HasForeignKey(x => x.InventoryBatchId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class StockAllocationConfiguration : IEntityTypeConfiguration<StockAllocation>
{
    public void Configure(EntityTypeBuilder<StockAllocation> b)
    {
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.UnitCostSnapshot).HasPrecision(18, 4);
        b.HasIndex(x => new { x.SaleItemId, x.InventoryBatchId });

        b.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.InventoryBatch).WithMany()
            .HasForeignKey(x => x.InventoryBatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SaleItem).WithMany(x => x.Allocations)
            .HasForeignKey(x => x.SaleItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.StockMovement).WithMany()
            .HasForeignKey(x => x.StockMovementId).OnDelete(DeleteBehavior.SetNull);
    }
}