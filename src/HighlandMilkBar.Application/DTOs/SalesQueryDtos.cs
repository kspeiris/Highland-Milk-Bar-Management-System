using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

/// <summary>Filter, sort and paging options for the sales history search.</summary>
public sealed class SaleSearchFilter
{
    public string? Search { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public SaleStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>One row in the sales history grid.</summary>
public sealed class SaleListRowDto
{
    public int Id { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime SoldAt { get; init; }
    public decimal ItemCount { get; init; }
    public string PaymentSummary { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public SaleStatus Status { get; init; }
    public string StatusText { get; init; } = string.Empty;
}

/// <summary>A line of a sale (used by the details view and receipt).</summary>
public sealed class SaleDetailItemDto
{
    public int SaleItemId { get; init; }
    public int? ProductId { get; init; }
    public string ItemNameSnapshot { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal UnitCostSnapshot { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal LineTotal { get; init; }
    public bool IsRecipeItem { get; init; }
}

/// <summary>A payment recorded against a sale.</summary>
public sealed class SaleDetailPaymentDto
{
    public PaymentMethod Method { get; init; }
    public string MethodText { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Reference { get; init; }
    public DateTime PaidAt { get; init; }
}

/// <summary>A return recorded against a sale.</summary>
public sealed class SaleDetailReturnDto
{
    public int SalesReturnId { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public DateTime ReturnedAt { get; init; }
    public decimal TotalAmount { get; init; }
    public bool Restocked { get; init; }
    public string StatusText { get; init; } = string.Empty;
}

/// <summary>Full sale record for the details view, receipt and reprint.</summary>
public sealed class SaleDetailDto
{
    public int Id { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime SoldAt { get; init; }
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal CostOfGoodsSold { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Change { get; init; }
    public SaleStatus Status { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public IReadOnlyList<SaleDetailItemDto> Items { get; init; } = Array.Empty<SaleDetailItemDto>();
    public IReadOnlyList<SaleDetailPaymentDto> Payments { get; init; } = Array.Empty<SaleDetailPaymentDto>();
    public IReadOnlyList<SaleDetailReturnDto> Returns { get; init; } = Array.Empty<SaleDetailReturnDto>();
}

/// <summary>Lightweight customer option for the optional POS customer picker.</summary>
public sealed class CustomerOptionDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
}