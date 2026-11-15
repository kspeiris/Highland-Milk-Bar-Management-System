using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Application.DTOs;

/// <summary>Sort options for the expense listing.</summary>
public enum ExpenseSort
{
    Date,
    Amount,
    Category
}

/// <summary>Filter, sort and paging options for the expense listing.</summary>
public sealed class ExpenseSearchFilter
{
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public ExpenseSort Sort { get; init; } = ExpenseSort.Date;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 15;
}

/// <summary>One expense row in the listing grid.</summary>
public sealed class ExpenseRowDto
{
    public int Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime ExpenseDate { get; init; }
    public PaymentMethod PaymentMethod { get; init; }
    public string PaymentMethodText { get; init; } = string.Empty;
    public string? ReferenceNumber { get; init; }
    public int? CashSessionId { get; init; }
}

/// <summary>A full expense record for the details view.</summary>
public sealed class ExpenseDetailDto
{
    public int Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime ExpenseDate { get; init; }
    public PaymentMethod PaymentMethod { get; init; }
    public string PaymentMethodText { get; init; } = string.Empty;
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
    public int? CashSessionId { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>Payload for creating an expense.</summary>
public sealed class ExpenseCreateRequest
{
    public int? CategoryId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime ExpenseDate { get; init; } = DateTime.Today;
    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Cash;
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
    public int? CashSessionId { get; init; }
}

/// <summary>Payload for editing an expense.</summary>
public sealed class ExpenseUpdateRequest
{
    public int Id { get; init; }
    public int? CategoryId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime ExpenseDate { get; init; }
    public PaymentMethod PaymentMethod { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
}

/// <summary>An expense category row.</summary>
public sealed class ExpenseCategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

/// <summary>Payload for creating/updating an expense category.</summary>
public sealed class ExpenseCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}

/// <summary>Summary counters shown above the expense grid.</summary>
public sealed class ExpenseStatsDto
{
    public decimal TotalExpenses { get; init; }
    public decimal MonthExpenses { get; init; }
    public decimal TodayExpenses { get; init; }
    public int ThisMonthCount { get; init; }
    public int CategoryCount { get; init; }
}

/// <summary>Spend for one category over a period.</summary>
public sealed class ExpenseCategoryBreakdownDto
{
    public int? CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int ExpenseCount { get; init; }
    public decimal Percentage { get; init; }
}

/// <summary>Total expenses for a calendar month.</summary>
public sealed class MonthlyExpenseDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string MonthLabel { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int ExpenseCount { get; init; }
}

/// <summary>Expense report for a date range.</summary>
public sealed class ExpenseReportDto
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public decimal TotalAmount { get; init; }
    public int ExpenseCount { get; init; }
    public decimal AverageAmount { get; init; }
    public IReadOnlyList<ExpenseCategoryBreakdownDto> ByCategory { get; init; } = Array.Empty<ExpenseCategoryBreakdownDto>();
}