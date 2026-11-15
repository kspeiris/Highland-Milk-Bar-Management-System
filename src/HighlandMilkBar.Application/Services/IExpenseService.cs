using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>Command-side operations for expenses and expense categories.</summary>
public interface IExpenseService
{
    Task<ExpenseDetailDto> CreateAsync(ExpenseCreateRequest request, CancellationToken cancellationToken = default);

    Task<ExpenseDetailDto> UpdateAsync(ExpenseUpdateRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int expenseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<ExpenseCategoryDto> CreateCategoryAsync(ExpenseCategoryRequest request, CancellationToken cancellationToken = default);

    Task<ExpenseCategoryDto> UpdateCategoryAsync(int categoryId, ExpenseCategoryRequest request, CancellationToken cancellationToken = default);

    Task SetCategoryActiveAsync(int categoryId, bool isActive, CancellationToken cancellationToken = default);
}