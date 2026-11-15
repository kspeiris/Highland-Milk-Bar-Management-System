using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

/// <summary>Sort options for the customer listing.</summary>
public enum CustomerSort
{
    Name,
    Spending,
    LastPurchase
}

/// <summary>Filter, sort and paging options for the customer listing.</summary>
public sealed class CustomerSearchFilter
{
    public string? Search { get; init; }
    public CustomerType? Type { get; init; }
    public bool? IsActive { get; init; }
    public CustomerSort Sort { get; init; } = CustomerSort.Name;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 15;
}

/// <summary>One customer row in the listing grid.</summary>
public sealed class CustomerListRowDto
{
    public int Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public CustomerType Type { get; init; }
    public string TypeText { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public int TotalPurchases { get; init; }
    public decimal TotalSpending { get; init; }
    public DateTime? LastPurchase { get; init; }
    public bool IsActive { get; init; }
    public string StatusText { get; init; } = string.Empty;
}

/// <summary>Summary counters shown above the customer grid.</summary>
public sealed class CustomerStatsDto
{
    public int TotalCustomers { get; init; }
    public int ActiveCustomers { get; init; }
    public int NewThisMonth { get; init; }
    public int RepeatCustomers { get; init; }
    public decimal TotalCustomerSales { get; init; }
}

/// <summary>A minimal customer record returned by create/update operations.</summary>
public sealed class CustomerDto
{
    public int Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public CustomerType Type { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Payload for creating a customer.</summary>
public sealed class CustomerCreateRequest
{
    public CustomerType Type { get; init; } = CustomerType.Individual;
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? AlternativePhone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>Payload for editing an existing customer.</summary>
public sealed class CustomerUpdateRequest
{
    public int Id { get; init; }
    public CustomerType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? AlternativePhone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>Profile overview statistics for a single customer.</summary>
public sealed class CustomerProfileDto
{
    public int Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public CustomerType Type { get; init; }
    public string TypeText { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? AlternativePhone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public int TotalPurchases { get; init; }
    public decimal TotalSpending { get; init; }
    public decimal AveragePurchaseValue { get; init; }
    public DateTime? LastPurchase { get; init; }
    public decimal TotalReturns { get; init; }
    public int PointsBalance { get; init; }
}

/// <summary>A single sale in a customer's purchase history.</summary>
public sealed class CustomerPurchaseHistoryRowDto
{
    public int SaleId { get; init; }
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime SoldAt { get; init; }
    public decimal TotalAmount { get; init; }
    public string PaymentMethodText { get; init; } = string.Empty;
    public SaleStatus Status { get; init; }
    public string StatusText { get; init; } = string.Empty;
}

/// <summary>Aggregated purchased product summary for a customer.</summary>
public sealed class CustomerPurchasedProductDto
{
    public int? ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal TotalQuantity { get; init; }
    public decimal TotalSpent { get; init; }
    public DateTime LastPurchased { get; init; }
}

/// <summary>A payment line for the customer's payments tab.</summary>
public sealed class CustomerPaymentDto
{
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime PaidAt { get; init; }
    public string MethodText { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Reference { get; init; }
}

/// <summary>A return line for the customer's returns tab.</summary>
public sealed class CustomerReturnDto
{
    public string ReturnNumber { get; init; } = string.Empty;
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime ReturnedAt { get; init; }
    public decimal TotalAmount { get; init; }
    public string StatusText { get; init; } = string.Empty;
}