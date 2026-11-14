namespace HighlandMilkBar.Domain.Entities;

public class InventoryBatch
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ManufacturedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal QuantityRemaining { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.Now;
}

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? InventoryBatchId { get; set; }
    public InventoryBatch? InventoryBatch { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal UnitCostSnapshot { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
