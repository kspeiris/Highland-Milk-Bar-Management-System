using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

/// <summary>
/// Management-accounting summary for a period. All figures are derived from existing
/// sales, returns, supplier payments and expense records — no separate ledger is written.
/// </summary>
public sealed class FinancialSummaryDto
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }

    // Profit & loss
    public decimal GrossSales { get; init; }
    public decimal SalesDiscounts { get; init; }
    public decimal SalesReturns { get; init; }
    public decimal NetSales { get; init; }
    public decimal CostOfGoodsSold { get; init; }
    public decimal GrossProfit { get; init; }
    public decimal OperatingExpenses { get; init; }
    public decimal OperatingProfit { get; init; }

    // Cash
    public decimal CashInflows { get; init; }
    public decimal CashOutflows { get; init; }
    public decimal OperatingCashFlow { get; init; }

    // Position
    public decimal SupplierOutstanding { get; init; }

    public decimal GrossMarginPercent { get; init; }
    public decimal NetMarginPercent { get; init; }
}

/// <summary>A line in the cash-flow statement.</summary>
public sealed class CashFlowLineDto
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int Count { get; init; }
}

/// <summary>Cash-flow summary with inflow/outflow breakdowns.</summary>
public sealed class CashFlowDto
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public decimal CashInflows { get; init; }
    public decimal CashOutflows { get; init; }
    public decimal NetCashFlow { get; init; }
    public IReadOnlyList<CashFlowLineDto> Inflows { get; init; } = Array.Empty<CashFlowLineDto>();
    public IReadOnlyList<CashFlowLineDto> Outflows { get; init; } = Array.Empty<CashFlowLineDto>();
}

/// <summary>Spend for one payment method over a period.</summary>
public sealed class PaymentBreakdownDto
{
    public PaymentMethod Method { get; init; }
    public string MethodText { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int TransactionCount { get; init; }
    public decimal Percentage { get; init; }
}

/// <summary>Outstanding balance for one supplier.</summary>
public sealed class SupplierLiabilityLineDto
{
    public int SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public decimal ReceivedValue { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal Outstanding { get; init; }
    public DateTime? EarliestDueDate { get; init; }
    public decimal OverdueAmount { get; init; }
    public string AgeingBucket { get; init; } = string.Empty;
}

/// <summary>One ageing bucket total.</summary>
public sealed class SupplierAgeingBucketDto
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int SupplierCount { get; init; }
}

/// <summary>Supplier liabilities as at a point in time.</summary>
public sealed class SupplierLiabilityDto
{
    public DateTime AsOf { get; init; }
    public decimal TotalReceived { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal TotalOutstanding { get; init; }
    public decimal OverdueTotal { get; init; }
    public int SupplierCount { get; init; }
    public IReadOnlyList<SupplierLiabilityLineDto> Lines { get; init; } = Array.Empty<SupplierLiabilityLineDto>();
    public IReadOnlyList<SupplierAgeingBucketDto> AgeingBuckets { get; init; } = Array.Empty<SupplierAgeingBucketDto>();
}

/// <summary>Cash-session reconciliation (open or closed).</summary>
public sealed class CashReconciliationDto
{
    public int SessionId { get; init; }
    public DateTime OpenedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public bool IsOpen { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public decimal OpeningFloat { get; init; }
    public decimal CashInflows { get; init; }
    public decimal CashOutflows { get; init; }
    public decimal ExpectedClosing { get; init; }
    public decimal? ActualClosing { get; init; }
    public decimal? Variance { get; init; }
}

/// <summary>One period point in a financial time series.</summary>
public sealed class FinancialSeriesPointDto
{
    public string PeriodLabel { get; init; } = string.Empty;
    public decimal GrossSales { get; init; }
    public decimal NetSales { get; init; }
    public decimal GrossProfit { get; init; }
    public decimal OperatingExpenses { get; init; }
    public decimal OperatingProfit { get; init; }
    public decimal OperatingCashFlow { get; init; }
}