namespace HighlandMilkBar.Application.DTOs;

/// <summary>Rule-based customer segments. A customer may carry more than one label.</summary>
public enum CustomerSegment
{
    New,
    Regular,
    HighValue,
    Inactive,
    OneTime
}

/// <summary>A customer with their computed segment labels.</summary>
public sealed class CustomerSegmentDto
{
    public int Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int TotalPurchases { get; init; }
    public decimal TotalSpending { get; init; }
    public DateTime? LastPurchase { get; init; }

    /// <summary>All segments the customer qualifies for.</summary>
    public IReadOnlyList<CustomerSegment> Segments { get; init; } = Array.Empty<CustomerSegment>();

    /// <summary>The single headline segment (see segmentation priority in the docs).</summary>
    public CustomerSegment PrimarySegment { get; init; }
    public string PrimarySegmentText { get; init; } = string.Empty;
}

/// <summary>Count of customers in a given segment.</summary>
public sealed class CustomerSegmentCountDto
{
    public CustomerSegment Segment { get; init; }
    public string SegmentText { get; init; } = string.Empty;
    public int Count { get; init; }
}

/// <summary>A ranked customer used by the top-customer lists.</summary>
public sealed class CustomerRankDto
{
    public int Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int TotalPurchases { get; init; }
    public decimal TotalSpending { get; init; }
    public DateTime? LastPurchase { get; init; }
}

/// <summary>Everything the customer analytics workspace displays.</summary>
public sealed class CustomerAnalyticsDto
{
    public int TotalCustomers { get; init; }
    public int ActiveCustomers { get; init; }
    public int NewThisMonth { get; init; }
    public int RepeatCustomers { get; init; }
    public decimal TotalCustomerSales { get; init; }
    public decimal AverageCustomerSpending { get; init; }

    public IReadOnlyList<CustomerRankDto> TopCustomersBySpending { get; init; } = Array.Empty<CustomerRankDto>();
    public IReadOnlyList<CustomerRankDto> TopCustomersByFrequency { get; init; } = Array.Empty<CustomerRankDto>();
    public IReadOnlyList<CustomerRankDto> InactiveCustomers { get; init; } = Array.Empty<CustomerRankDto>();
    public IReadOnlyList<CustomerSegmentCountDto> SegmentCounts { get; init; } = Array.Empty<CustomerSegmentCountDto>();

    /// <summary>Thresholds used to derive the segmentation (documented in CUSTOMER_ANALYTICS.md).</summary>
    public int NewCustomerDays { get; init; } = 30;
    public int RegularMinPurchases { get; init; } = 5;
    public int InactiveDays { get; init; } = 90;
    public decimal HighValueThreshold { get; init; }
}