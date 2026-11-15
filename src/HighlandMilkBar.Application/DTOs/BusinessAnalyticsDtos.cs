using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

/// <summary>A current-vs-previous comparison with safe percentage handling.</summary>
public sealed class ComparisonDto
{
    public decimal Current { get; init; }
    public decimal Previous { get; init; }
    public decimal Difference { get; init; }

    /// <summary>Null when there is no meaningful baseline (previous value is zero).</summary>
    public decimal? PercentChange { get; init; }

    public bool IsNewActivity { get; init; }
    public bool IsIncrease { get; init; }

    public string ChangeText { get; init; } = string.Empty;
}

/// <summary>One point in a sales trend (day or month).</summary>
public sealed class SalesTrendPointDto
{
    public string Label { get; init; } = string.Empty;
    public decimal NetSales { get; init; }
    public int Orders { get; init; }
}

/// <summary>Revenue for one product category.</summary>
public sealed class CategorySalesDto
{
    public string CategoryName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal Units { get; init; }
    public decimal Percentage { get; init; }
}

/// <summary>Revenue for one weekday.</summary>
public sealed class WeekdaySalesDto
{
    public string Day { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int Orders { get; init; }
}

/// <summary>One product's sales performance.</summary>
public sealed class ProductPerformanceDto
{
    public int ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public decimal UnitsSold { get; init; }
    public decimal Revenue { get; init; }
    public decimal? GrossProfit { get; init; }
}

/// <summary>Sales analytics for a reporting period.</summary>
public sealed class SalesAnalyticsDto
{
    public DateTime Start { get; init; }
    public DateTime EndExclusive { get; init; }
    public decimal GrossSales { get; init; }
    public decimal NetSales { get; init; }
    public int Orders { get; init; }
    public decimal AverageOrderValue { get; init; }
    public decimal UnitsSold { get; init; }
    public string? BestDay { get; init; }

    public IReadOnlyList<SalesTrendPointDto> Trend { get; init; } = Array.Empty<SalesTrendPointDto>();
    public IReadOnlyList<CategorySalesDto> ByCategory { get; init; } = Array.Empty<CategorySalesDto>();
    public IReadOnlyList<PaymentBreakdownDto> ByPayment { get; init; } = Array.Empty<PaymentBreakdownDto>();
    public IReadOnlyList<WeekdaySalesDto> ByWeekday { get; init; } = Array.Empty<WeekdaySalesDto>();
}

/// <summary>Product performance analytics for a reporting period.</summary>
public sealed class ProductAnalyticsDto
{
    public IReadOnlyList<ProductPerformanceDto> TopSellers { get; init; } = Array.Empty<ProductPerformanceDto>();
    public IReadOnlyList<ProductPerformanceDto> HighProfit { get; init; } = Array.Empty<ProductPerformanceDto>();
    public IReadOnlyList<ProductPerformanceDto> LowPerformers { get; init; } = Array.Empty<ProductPerformanceDto>();
    public IReadOnlyList<ProductPerformanceDto> NoSale { get; init; } = Array.Empty<ProductPerformanceDto>();
}

/// <summary>Severity of a business alert.</summary>
public enum BusinessAlertSeverity
{
    Information,
    Warning,
    Critical
}

/// <summary>Category of a business alert.</summary>
public enum BusinessAlertCategory
{
    Inventory,
    Sales,
    Customer,
    Recipe,
    Purchase,
    Expense,
    Finance
}

/// <summary>A rule-based, explainable operational alert.</summary>
public sealed class BusinessAlertDto
{
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public BusinessAlertSeverity Severity { get; init; }
    public BusinessAlertCategory Category { get; init; }
    public string Metric { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public int? RelatedRecordId { get; init; }
    public string NavigationTarget { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.Now;

    public string SeverityText => Severity.ToString().ToUpperInvariant();
    public string CategoryText => Category.ToString();

    /// <summary>Deterministic identity so refreshes do not duplicate the same alert.</summary>
    public string Key => $"{Type}:{RelatedRecordId?.ToString() ?? "-"}";
}

/// <summary>Everything the business overview dashboard shows.</summary>
public sealed class BusinessOverviewDto
{
    public DateTime Start { get; init; }
    public DateTime EndExclusive { get; init; }
    public string PeriodLabel { get; init; } = string.Empty;

    public FinancialSummaryDto Financial { get; init; } = new();

    public int Orders { get; init; }
    public decimal UnitsSold { get; init; }
    public decimal AverageOrderValue { get; init; }

    public int TotalCustomers { get; init; }
    public int NewCustomers { get; init; }
    public int RepeatCustomers { get; init; }

    public InventoryOverviewDto Inventory { get; init; } = new();

    public ComparisonDto NetSalesComparison { get; init; } = new();
    public ComparisonDto GrossProfitComparison { get; init; } = new();
    public ComparisonDto ExpensesComparison { get; init; } = new();
    public ComparisonDto OrdersComparison { get; init; } = new();

    public IReadOnlyList<ProductPerformanceDto> TopProducts { get; init; } = Array.Empty<ProductPerformanceDto>();
    public IReadOnlyList<CategorySalesDto> CategorySales { get; init; } = Array.Empty<CategorySalesDto>();
    public IReadOnlyList<SalesTrendPointDto> Trend { get; init; } = Array.Empty<SalesTrendPointDto>();
    public IReadOnlyList<BusinessAlertDto> Alerts { get; init; } = Array.Empty<BusinessAlertDto>();
}