using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

public sealed class BatchDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string BatchNumber { get; init; } = string.Empty;
    public DateTime? ExpiryDate { get; init; }
    public decimal QuantityRemaining { get; init; }
    public decimal UnitCost { get; init; }
}

/// <summary>Records a receipt of stock that creates a new inventory batch.</summary>
public sealed class StockReceiptRequest
{
    public int ProductId { get; init; }
    public string BatchNumber { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public DateTime? ManufacturedDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public int? SupplierId { get; init; }
    public int? PurchaseItemId { get; init; }
    public string? Note { get; init; }

    /// <summary>When set, overrides the recorded received date (defaults to now).</summary>
    public DateTime? ReceivedDate { get; init; }

    /// <summary>Business reason for the receipt. When omitted it is derived from PurchaseItemId.</summary>
    public StockReceiptReason Reason { get; init; } = StockReceiptReason.ManualReceipt;

    /// <summary>Optional external reference (e.g. GRN or invoice number).</summary>
    public string? ReferenceNumber { get; init; }
}

public sealed class StockAdjustmentLine
{
    public int ProductId { get; init; }
    public int? InventoryBatchId { get; init; }
    public decimal QuantityChange { get; init; }
    public decimal UnitCost { get; init; }
    public string? Note { get; init; }
    public StockAdjustmentKind Kind { get; init; } = StockAdjustmentKind.Correction;
}

/// <summary>A manual stock correction applied atomically across one or more products.</summary>
public sealed class StockAdjustmentRequest
{
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public IReadOnlyList<StockAdjustmentLine> Lines { get; init; } = new List<StockAdjustmentLine>();
}

/// <summary>The result of allocating stock across batches for a movement.</summary>
public sealed class AllocationResult
{
    public decimal QuantityAllocated { get; init; }
    public decimal TotalCost { get; init; }
    public IReadOnlyList<AllocationLine> Lines { get; init; } = new List<AllocationLine>();
}

public sealed class AllocationLine
{
    public int InventoryBatchId { get; init; }
    public string BatchNumber { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
}

/// <summary>A durable record that a batch was consumed/returned by a sale line.</summary>
public sealed class StockAllocationDto
{
    public int InventoryBatchId { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCostSnapshot { get; init; }
    public StockMovementType MovementType { get; init; }
}