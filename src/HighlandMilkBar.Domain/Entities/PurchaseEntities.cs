using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Domain.Entities;

public class Purchase
{
    public int Id { get; set; }

    /// <summary>Internal, unique purchase number (e.g. PUR-000001).</summary>
    public string PurchaseNumber { get; set; } = string.Empty;

    /// <summary>Supplier's invoice number for this purchase. Blank means none supplied. Unique per supplier when non-blank.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Invoice / purchase date.</summary>
    public DateTime PurchaseDate { get; set; } = DateTime.Today;

    /// <summary>Date payment is due to the supplier.</summary>
    public DateTime? DueDate { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AdditionalCharges { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }

    /// <summary>Receiving lifecycle (Draft / Received / Cancelled).</summary>
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;

    /// <summary>Settlement state derived from allocated payments.</summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Business date the goods were received (set on receive).</summary>
    public DateTime? ReceivedDate { get; set; }

    /// <summary>Audit timestamp of when the purchase was received in the system.</summary>
    public DateTime? ReceivedAt { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    public ICollection<SupplierPayment> SupplierPayments { get; set; } = new List<SupplierPayment>();
}

public class PurchaseItem
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    /// <summary>Per-line discount amount (not a percentage).</summary>
    public decimal DiscountAmount { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>Purchase unit label snapshot (e.g. "Bottle").</summary>
    public string? Unit { get; set; }

    public string? BatchNumber { get; set; }
    public DateTime? ManufacturedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Quantity actually received (defaults to ordered quantity on receipt).</summary>
    public decimal ReceivedQuantity { get; set; }

    public ICollection<InventoryBatch> InventoryBatches { get; set; } = new List<InventoryBatch>();
}

/// <summary>A payment made to a supplier against a purchase.</summary>
public class SupplierPayment
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public DateTime PaidAt { get; set; } = DateTime.Now;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}