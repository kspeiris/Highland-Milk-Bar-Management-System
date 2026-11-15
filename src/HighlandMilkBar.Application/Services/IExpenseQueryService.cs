using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>Read-only expense queries: listing, detail, dashboard, analytics and reports.</summary>
public interface IExpenseQueryService
{
    Task<PagedResult<ExpenseRowDto>> SearchAsync(ExpenseSearchFilter filter, CancellationToken cancellationToken = default);

    Task<ExpenseDetailDto?> GetByIdAsync(int expenseId, CancellationToken cancellationToken = default);

    Task<ExpenseStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseCategoryBreakdownDto>> GetCategoryBreakdownAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonthlyExpenseDto>> GetMonthlyTrendAsync(int months = 6, CancellationToken cancellationToken = default);

    Task<ExpenseReportDto> GetReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}