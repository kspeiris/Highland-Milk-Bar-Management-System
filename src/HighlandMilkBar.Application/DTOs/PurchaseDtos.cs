using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

public sealed class ReceivePurchaseResult
{
    public int PurchaseId { get; init; }
    public int BatchesCreated { get; init; }
    public int MovementsRecorded { get; init; }
    public decimal TotalQuantityReceived { get; init; }
}

public sealed class BackupResult
{
    public string Path { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>Receiving-lifecycle + settlement filter for the purchase list.</summary>
public sealed class PurchaseFilter
{
    public string? Search { get; init; }
    public int? SupplierId { get; init; }
    public PurchaseStatus? ReceiveStatus { get; init; }
    public PaymentStatus? PaymentStatus { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>One purchase row in the listing.</summary>
public sealed class PurchaseRowDto
{
    public int Id { get; init; }
    public string PurchaseNumber { get; init; } = string.Empty;
    public string? InvoiceNumber { get; init; }
    public int? SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public DateTime PurchaseDate { get; init; }
    public DateTime? ReceivedDate { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Outstanding { get; init; }
    public PurchaseStatus ReceiveStatus { get; init; }
    public PaymentStatus PaymentStatus { get; init; }
    public int ItemCount { get; init; }
}

/// <summary>A single line of a purchase.</summary>
public sealed class PurchaseItemDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ProductSku { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal LineTotal { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public bool RequiresExpiryDate { get; init; }
    public bool TrackInventory { get; init; }
}

/// <summary>Full purchase record for the details / receive screens.</summary>
public sealed class PurchaseDetailDto
{
    public int Id { get; init; }
    public string PurchaseNumber { get; init; } = string.Empty;
    public string? InvoiceNumber { get; init; }
    public int? SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public DateTime PurchaseDate { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? ReceivedDate { get; init; }
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal AdditionalCharges { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Outstanding { get; init; }
    public PurchaseStatus ReceiveStatus { get; init; }
    public PaymentStatus PaymentStatus { get; init; }
    public bool CanReceive { get; init; }
    public bool CanEdit { get; init; }
    public string? Notes { get; init; }

    public IReadOnlyList<PurchaseItemDto> Items { get; init; } = Array.Empty<PurchaseItemDto>();
    public IReadOnlyList<SupplierPaymentDto> Payments { get; init; } = Array.Empty<SupplierPaymentDto>();
}

/// <summary>A purchase line in a create/update request.</summary>
public sealed class PurchaseLineRequest
{
    public int ProductId { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public decimal DiscountAmount { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ExpiryDate { get; init; }
}

/// <summary>Request to create a purchase (as a draft).</summary>
public sealed class CreatePurchaseRequest
{
    public int SupplierId { get; init; }
    public string? InvoiceNumber { get; init; }
    public DateTime PurchaseDate { get; init; } = DateTime.Today;
    public DateTime? DueDate { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal AdditionalCharges { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<PurchaseLineRequest> Items { get; init; } = Array.Empty<PurchaseLineRequest>();
}

/// <summary>Request to update a draft purchase.</summary>
public sealed class UpdatePurchaseRequest
{
    public int Id { get; init; }
    public int SupplierId { get; init; }
    public string? InvoiceNumber { get; init; }
    public DateTime PurchaseDate { get; init; }
    public DateTime? DueDate { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal AdditionalCharges { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<PurchaseLineRequest> Items { get; init; } = Array.Empty<PurchaseLineRequest>();
}

/// <summary>A supplier payment against a purchase.</summary>
public sealed class SupplierPaymentDto
{
    public int Id { get; init; }
    public int PurchaseId { get; init; }
    public string PurchaseNumber { get; init; } = string.Empty;
    public int? SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public PaymentMethod Method { get; init; }
    public DateTime PaidAt { get; init; }
    public string? Reference { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Request to record a payment against a received purchase.</summary>
public sealed class RecordPaymentRequest
{
    public int PurchaseId { get; init; }
    public decimal Amount { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.Cash;
    public DateTime PaidAt { get; init; } = DateTime.Now;
    public string? Reference { get; init; }
    public string? Notes { get; init; }
}

/// <summary>An outstanding balance row (per supplier or per purchase).</summary>
public sealed class OutstandingBalanceDto
{
    public int? SupplierId { get; init; }
    public int? PurchaseId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public string PurchaseNumber { get; init; } = string.Empty;
    public DateTime PurchaseDate { get; init; }
    public DateTime? DueDate { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Outstanding { get; init; }
    public bool IsOverdue { get; init; }
}

/// <summary>A historical purchase price for one product.</summary>
public sealed class PriceHistoryRowDto
{
    public int PurchaseId { get; init; }
    public string PurchaseNumber { get; init; } = string.Empty;
    public string? InvoiceNumber { get; init; }
    public int? SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public DateTime PurchaseDate { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
}

/// <summary>Price-history summary for one product.</summary>
public sealed class PriceHistoryDto
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal? LatestCost { get; init; }
    public decimal? LowestCost { get; init; }
    public decimal? HighestCost { get; init; }
    public int PurchaseCount { get; init; }
    public IReadOnlyList<PriceHistoryRowDto> Rows { get; init; } = Array.Empty<PriceHistoryRowDto>();
}

// ---------- Dashboard ----------

/// <summary>One supplier's purchase value for the dashboard chart.</summary>
public sealed class SupplierSpendDto
{
    public string SupplierName { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public int Purchases { get; init; }
}

/// <summary>One category's purchase value for the dashboard chart.</summary>
public sealed class CategorySpendDto
{
    public string CategoryName { get; init; } = string.Empty;
    public decimal Value { get; init; }
}

/// <summary>A product's total purchased quantity for the dashboard chart.</summary>
public sealed class TopPurchasedDto
{
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Value { get; init; }
}

/// <summary>One month's purchase value for the trend chart.</summary>
public sealed class MonthlySpendDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Label { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public int Purchases { get; init; }
}

/// <summary>KPIs, charts and recent rows for the purchase dashboard.</summary>
public sealed class PurchaseDashboardDto
{
    public int TotalReceivedPurchases { get; init; }
    public decimal PurchaseValue { get; init; }
    public int ActiveSuppliers { get; init; }
    public decimal OutstandingPayments { get; init; }
    public decimal PaymentsThisMonth { get; init; }
    public int PurchasesThisMonth { get; init; }
    public decimal PurchasesThisMonthValue { get; init; }

    public int UnpaidPurchases { get; init; }
    public int PartiallyPaidPurchases { get; init; }
    public int PaidPurchases { get; init; }

    public IReadOnlyList<MonthlySpendDto> MonthlyTrend { get; init; } = Array.Empty<MonthlySpendDto>();
    public IReadOnlyList<SupplierSpendDto> BySupplier { get; init; } = Array.Empty<SupplierSpendDto>();
    public IReadOnlyList<CategorySpendDto> ByCategory { get; init; } = Array.Empty<CategorySpendDto>();
    public IReadOnlyList<TopPurchasedDto> TopPurchased { get; init; } = Array.Empty<TopPurchasedDto>();
    public IReadOnlyList<PurchaseRowDto> RecentPurchases { get; init; } = Array.Empty<PurchaseRowDto>();
}