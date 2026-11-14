using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Domain.Entities;

public class Sale
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime SoldAt { get; set; } = DateTime.Now;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? CashSessionId { get; set; }
    public CashSession? CashSession { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Total cost of goods sold captured from line cost snapshots at sale time.</summary>
    public decimal CostOfGoodsSold { get; set; }

    /// <summary>Total tendered across all payments (may exceed total; change is derived).</summary>
    public decimal AmountPaid { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<SalesReturn> SalesReturns { get; set; } = new List<SalesReturn>();
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Name captured at sale time so later product renames do not rewrite history.</summary>
    public string ItemNameSnapshot { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCostSnapshot { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>For prepared items, the recipe version used (for cost/stock traceability).</summary>
    public int? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public ICollection<StockAllocation> Allocations { get; set; } = new List<StockAllocation>();
    public ICollection<SalesReturnItem> ReturnItems { get; set; } = new List<SalesReturnItem>();
}

public class Payment
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.Now;
    public string? Reference { get; set; }
}

public class SalesReturn
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime ReturnedAt { get; set; } = DateTime.Now;
    public string? Reason { get; set; }
    public decimal TotalAmount { get; set; }
    public bool Restocked { get; set; }
    public SalesReturnStatus Status { get; set; } = SalesReturnStatus.Completed;
    public string? Notes { get; set; }

    public ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
}

public class SalesReturnItem
{
    public int Id { get; set; }
    public int SalesReturnId { get; set; }
    public SalesReturn? SalesReturn { get; set; }
    public int? SaleItemId { get; set; }
    public SaleItem? SaleItem { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    public string ItemNameSnapshot { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public ReturnCondition Condition { get; set; } = ReturnCondition.Restock;
    public decimal LineTotal { get; set; }
}