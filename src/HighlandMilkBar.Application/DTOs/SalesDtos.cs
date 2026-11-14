using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

public sealed class SaleLineRequest
{
    public int ProductId { get; init; }
    public string? ItemNameSnapshot { get; init; }
    public decimal Quantity { get; init; } = 1;
    public decimal UnitPrice { get; init; }
    public decimal DiscountAmount { get; init; }
}

public sealed class PaymentRequest
{
    public PaymentMethod Method { get; init; }
    public decimal Amount { get; init; }
    public string? Reference { get; init; }
}

public sealed class SaleCheckoutRequest
{
    public int? CustomerId { get; init; }
    public int? CashSessionId { get; init; }
    public decimal OrderDiscount { get; init; }
    public IReadOnlyList<SaleLineRequest> Items { get; init; } = new List<SaleLineRequest>();
    public IReadOnlyList<PaymentRequest> Payments { get; init; } = new List<PaymentRequest>();
    public AllocationPolicy AllocationPolicy { get; init; } = AllocationPolicy.Fefo;
    public string? Notes { get; init; }
}

public sealed class SaleResult
{
    public int SaleId { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal CostOfGoodsSold { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Change { get; init; }
}

public sealed class ReturnLineRequest
{
    public int SaleItemId { get; init; }
    public decimal Quantity { get; init; }
    public ReturnCondition Condition { get; init; } = ReturnCondition.Restock;
}

public sealed class SalesReturnRequest
{
    public int SaleId { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<ReturnLineRequest> Lines { get; init; } = new List<ReturnLineRequest>();
}

public sealed class SalesReturnResult
{
    public int SalesReturnId { get; init; }
    public string ReturnNumber { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public bool Restocked { get; init; }
}