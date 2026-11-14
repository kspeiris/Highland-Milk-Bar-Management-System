using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Domain.Entities;

/// <summary>
/// A received quantity of a product at a specific cost and expiry. Batch stock is
/// consumed using FIFO or FEFO allocation and every change is traceable via
/// <see cref="StockMovement"/> and <see cref="StockAllocation"/>.
/// </summary>
public class InventoryBatch
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int? PurchaseItemId { get; set; }
    public PurchaseItem? PurchaseItem { get; set; }

    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ManufacturedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityRemaining { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>
/// The immutable stock ledger. One row is written for every quantity change so the
/// current stock of a product/batch can always be reconciled from history.
/// </summary>
public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? InventoryBatchId { get; set; }
    public InventoryBatch? InventoryBatch { get; set; }

    public StockMovementType MovementType { get; set; }
    public decimal QuantityChange { get; set; }
    public decimal UnitCostSnapshot { get; set; }
    public decimal BalanceAfter { get; set; }
    public MovementReferenceType? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>A manual stock correction (count, damage, waste) applied in one action.</summary>
public class StockAdjustment
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime AdjustmentDate { get; set; } = DateTime.Today;
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public StockAdjustmentStatus Status { get; set; } = StockAdjustmentStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ICollection<StockAdjustmentItem> Items { get; set; } = new List<StockAdjustmentItem>();
}

public class StockAdjustmentItem
{
    public int Id { get; set; }
    public int StockAdjustmentId { get; set; }
    public StockAdjustment? StockAdjustment { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? InventoryBatchId { get; set; }
    public InventoryBatch? InventoryBatch { get; set; }
    public decimal QuantityChange { get; set; }
    public decimal UnitCostSnapshot { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Records exactly which batches (and how much from each) were consumed or returned
/// for a sale line. This is the audit trail that proves FIFO/FEFO behaviour.
/// </summary>
public class StockAllocation
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int InventoryBatchId { get; set; }
    public InventoryBatch? InventoryBatch { get; set; }
    public int? SaleItemId { get; set; }
    public SaleItem? SaleItem { get; set; }

    public StockMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCostSnapshot { get; set; }
    public int? StockMovementId { get; set; }
    public StockMovement? StockMovement { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}