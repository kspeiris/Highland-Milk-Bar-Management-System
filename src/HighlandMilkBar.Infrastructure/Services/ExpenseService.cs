using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Command and query implementation for expenses and expense categories.
/// Recording an expense only writes <c>Expenses</c> (and an optional cash movement);
/// it never touches sales, supplier balances or inventory.
/// </summary>
public sealed class ExpenseService(AppDbContext db) : IExpenseService, IExpenseQueryService
{
    // ---------------- Commands ----------------

    public async Task<ExpenseDetailDto> CreateAsync(ExpenseCreateRequest request, CancellationToken cancellationToken = default)
    {
        var description = RequireDescription(request.Description);
        if (request.Amount <= 0m)
            throw new ArgumentException("An expense amount greater than zero is required.", nameof(request));
        await ValidateCategoryAsync(request.CategoryId, cancellationToken);
        await ValidateCashSessionAsync(request.CashSessionId, cancellationToken);

        var expense = new Expense
        {
            ExpenseCategoryId = request.CategoryId,
            Description = description,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = NullIfBlank(request.ReferenceNumber),
            Notes = NullIfBlank(request.Notes),
            CashSessionId = request.CashSessionId,
            CreatedAt = DateTime.Now
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        // Only a cash expense moves money out of the drawer; card/bank expenses never
        // affect the cash session even when one is linked for reporting.
        if (request.CashSessionId is { } sessionId && request.PaymentMethod == PaymentMethod.Cash)
        {
            db.CashMovements.Add(new CashMovement
            {
                CashSessionId = sessionId,
                MovementType = CashMovementType.Expense,
                Direction = CashDirection.Out,
                Amount = expense.Amount,
                ReferenceId = expense.Id,
                Note = expense.Description,
                CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(expense.Id, cancellationToken) ?? throw new InvalidOperationException("Expense not found after creation.");
    }

    public async Task<ExpenseDetailDto> UpdateAsync(ExpenseUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Expense {request.Id} was not found.");

        var description = RequireDescription(request.Description);
        if (request.Amount <= 0m)
            throw new ArgumentException("An expense amount greater than zero is required.", nameof(request));
        await ValidateCategoryAsync(request.CategoryId, cancellationToken);

        expense.ExpenseCategoryId = request.CategoryId;
        expense.Description = description;
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        expense.PaymentMethod = request.PaymentMethod;
        expense.ReferenceNumber = NullIfBlank(request.ReferenceNumber);
        expense.Notes = NullIfBlank(request.Notes);

        await SyncCashMovementAsync(expense, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(expense.Id, cancellationToken) ?? throw new InvalidOperationException("Expense not found after update.");
    }

    public async Task DeleteAsync(int expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId, cancellationToken)
            ?? throw new InvalidOperationException($"Expense {expenseId} was not found.");

        // Remove the linked cash movement (if any) so the drawer history stays consistent.
        var movements = await db.CashMovements
            .Where(m => m.MovementType == CashMovementType.Expense && m.ReferenceId == expenseId)
            .ToListAsync(cancellationToken);
        if (movements.Count > 0) db.CashMovements.RemoveRange(movements);

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await db.ExpenseCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto { Id = c.Id, Name = c.Name, IsActive = c.IsActive })
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpenseCategoryDto> CreateCategoryAsync(ExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = RequireName(request.Name);
        if (await db.ExpenseCategories.AnyAsync(c => c.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new InvalidOperationException($"An expense category named '{name}' already exists.");

        var category = new ExpenseCategory { Name = name, IsActive = request.IsActive };
        db.ExpenseCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return new ExpenseCategoryDto { Id = category.Id, Name = category.Name, IsActive = category.IsActive };
    }

    public async Task<ExpenseCategoryDto> UpdateCategoryAsync(int categoryId, ExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            ?? throw new InvalidOperationException($"Expense category {categoryId} was not found.");
        var name = RequireName(request.Name);
        if (await db.ExpenseCategories.AnyAsync(c => c.Id != categoryId && c.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new InvalidOperationException($"An expense category named '{name}' already exists.");

        category.Name = name;
        category.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return new ExpenseCategoryDto { Id = category.Id, Name = category.Name, IsActive = category.IsActive };
    }

    public async Task SetCategoryActiveAsync(int categoryId, bool isActive, CancellationToken cancellationToken = default)
    {
        var category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            ?? throw new InvalidOperationException($"Expense category {categoryId} was not found.");
        category.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------------- Queries ----------------

    public async Task<PagedResult<ExpenseRowDto>> SearchAsync(ExpenseSearchFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new ExpenseSearchFilter();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 200 ? 15 : filter.PageSize;

        var query = db.Expenses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var like = $"%{EscapeLike(filter.Search)}%";
            query = query.Where(e => EF.Functions.Like(e.Description, like)
                || (e.ReferenceNumber != null && EF.Functions.Like(e.ReferenceNumber, like))
                || (e.ExpenseCategory != null && EF.Functions.Like(e.ExpenseCategory.Name, like)));
        }
        if (filter.CategoryId is { } cid) query = query.Where(e => e.ExpenseCategoryId == cid);
        if (filter.PaymentMethod is { } pm) query = query.Where(e => e.PaymentMethod == pm);
        if (filter.From is { } from) query = query.Where(e => e.ExpenseDate >= from);
        if (filter.To is { } to) query = query.Where(e => e.ExpenseDate < to.AddDays(1));

        var total = await query.CountAsync(cancellationToken);

        var ordered = filter.Sort switch
        {
            ExpenseSort.Amount => query.OrderByDescending(e => e.Amount),
            ExpenseSort.Category => query.OrderBy(e => e.ExpenseCategory!.Name).ThenByDescending(e => e.ExpenseDate),
            _ => query.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id)
        };

        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var items = rows.Select(ToRow).ToList();
        return new PagedResult<ExpenseRowDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<ExpenseDetailDto?> GetByIdAsync(int expenseId, CancellationToken cancellationToken = default)
    {
        var e = await db.Expenses.AsNoTracking()
            .Where(x => x.Id == expenseId)
            .Select(x => new ExpenseDetailDto
            {
                Id = x.Id,
                Description = x.Description,
                CategoryId = x.ExpenseCategoryId,
                CategoryName = x.ExpenseCategory != null ? x.ExpenseCategory.Name : string.Empty,
                Amount = x.Amount,
                ExpenseDate = x.ExpenseDate,
                PaymentMethod = x.PaymentMethod,
                PaymentMethodText = FormatPaymentMethod(x.PaymentMethod),
                ReferenceNumber = x.ReferenceNumber,
                Notes = x.Notes,
                CashSessionId = x.CashSessionId,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
        return e;
    }

    public async Task<ExpenseStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var todayStart = now.Date;
        var todayEnd = todayStart.AddDays(1);

        var total = await db.Expenses.SumAsync(e => e.Amount, cancellationToken);
        var monthEnd = monthStart.AddMonths(1);
        var month = await db.Expenses.Where(e => e.ExpenseDate >= monthStart && e.ExpenseDate < monthEnd).SumAsync(e => e.Amount, cancellationToken);
        var today = await db.Expenses.Where(e => e.ExpenseDate >= todayStart && e.ExpenseDate < todayEnd).SumAsync(e => e.Amount, cancellationToken);
        var monthCount = await db.Expenses.CountAsync(e => e.ExpenseDate >= monthStart && e.ExpenseDate < monthEnd, cancellationToken);
        var categoryCount = await db.ExpenseCategories.CountAsync(cancellationToken);

        return new ExpenseStatsDto
        {
            TotalExpenses = total,
            MonthExpenses = month,
            TodayExpenses = today,
            ThisMonthCount = monthCount,
            CategoryCount = categoryCount
        };
    }

    public async Task<IReadOnlyList<ExpenseCategoryBreakdownDto>> GetCategoryBreakdownAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var query = db.Expenses.AsNoTracking().AsQueryable();
        if (from is { } f) query = query.Where(e => e.ExpenseDate >= f);
        if (to is { } t) query = query.Where(e => e.ExpenseDate < t.AddDays(1));

        var groups = await query
            .GroupBy(e => new { e.ExpenseCategoryId, Name = e.ExpenseCategory != null ? e.ExpenseCategory.Name : "Uncategorized" })
            .Select(g => new { g.Key.ExpenseCategoryId, g.Key.Name, Total = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var grand = groups.Sum(g => g.Total);
        return groups
            .OrderByDescending(g => g.Total)
            .Select(g => new ExpenseCategoryBreakdownDto
            {
                CategoryId = g.ExpenseCategoryId,
                CategoryName = g.Name,
                TotalAmount = g.Total,
                ExpenseCount = g.Count,
                Percentage = grand == 0m ? 0m : Math.Round(g.Total / grand * 100m, 1)
            })
            .ToList();
    }

    public async Task<IReadOnlyList<MonthlyExpenseDto>> GetMonthlyTrendAsync(int months = 6, CancellationToken cancellationToken = default)
    {
        if (months < 1) months = 6;
        if (months > 24) months = 24;

        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));
        var rows = await db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= start)
            .Select(e => new { e.ExpenseDate, e.Amount })
            .ToListAsync(cancellationToken);

        var result = new List<MonthlyExpenseDto>();
        for (var i = 0; i < months; i++)
        {
            var month = start.AddMonths(i);
            var monthRows = rows.Where(r => r.ExpenseDate.Year == month.Year && r.ExpenseDate.Month == month.Month).ToList();
            result.Add(new MonthlyExpenseDto
            {
                Year = month.Year,
                Month = month.Month,
                MonthLabel = month.ToString("yyyy-MM"),
                TotalAmount = monthRows.Sum(r => r.Amount),
                ExpenseCount = monthRows.Count
            });
        }
        return result;
    }

    public async Task<ExpenseReportDto> GetReportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);
        var query = db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= from && e.ExpenseDate < to.AddDays(1));
        var total = await query.SumAsync(e => e.Amount, cancellationToken);
        var count = await query.CountAsync(cancellationToken);
        var breakdown = await GetCategoryBreakdownAsync(from, to, cancellationToken);

        return new ExpenseReportDto
        {
            From = from,
            To = to,
            TotalAmount = total,
            ExpenseCount = count,
            AverageAmount = count == 0 ? 0m : total / count,
            ByCategory = breakdown
        };
    }

    // ---------------- Helpers ----------------

    private async Task ValidateCategoryAsync(int? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is not { } id) return;
        if (!await db.ExpenseCategories.AnyAsync(c => c.Id == id, cancellationToken))
            throw new InvalidOperationException($"Expense category {id} was not found.");
    }

    private async Task ValidateCashSessionAsync(int? cashSessionId, CancellationToken cancellationToken)
    {
        if (cashSessionId is not { } id) return;
        var session = await db.CashSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Cash session {id} was not found.");
        if (session.Status != CashSessionStatus.Open)
            throw new InvalidOperationException("An expense can only be linked to an open cash session.");
    }

    // Keep the drawer ledger in step with the expense: exactly one cash-out movement when the
    // expense is cash against an open session, and none when it is non-cash or unlinked.
    private async Task SyncCashMovementAsync(Expense expense, CancellationToken cancellationToken)
    {
        var existing = await db.CashMovements
            .Where(m => m.MovementType == CashMovementType.Expense && m.ReferenceId == expense.Id)
            .ToListAsync(cancellationToken);

        if (expense.PaymentMethod == PaymentMethod.Cash && expense.CashSessionId is { } sessionId)
        {
            if (existing.Count == 0)
            {
                db.CashMovements.Add(new CashMovement
                {
                    CashSessionId = sessionId,
                    MovementType = CashMovementType.Expense,
                    Direction = CashDirection.Out,
                    Amount = expense.Amount,
                    ReferenceId = expense.Id,
                    Note = expense.Description,
                    CreatedAt = DateTime.Now
                });
                return;
            }

            var movement = existing[0];
            movement.CashSessionId = sessionId;
            movement.Direction = CashDirection.Out;
            movement.Amount = expense.Amount;
            movement.Note = expense.Description;
            for (var i = 1; i < existing.Count; i++)
                db.CashMovements.Remove(existing[i]);
        }
        else if (existing.Count > 0)
        {
            db.CashMovements.RemoveRange(existing);
        }
    }

    private static string RequireDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("An expense description is required.", nameof(description));
        return trimmed;
    }

    private static string RequireName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("A category name is required.", nameof(name));
        return trimmed;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ExpenseRowDto ToRow(Expense e) => new()
    {
        Id = e.Id,
        Description = e.Description,
        CategoryId = e.ExpenseCategoryId,
        CategoryName = e.ExpenseCategory != null ? e.ExpenseCategory.Name : string.Empty,
        Amount = e.Amount,
        ExpenseDate = e.ExpenseDate,
        PaymentMethod = e.PaymentMethod,
        PaymentMethodText = FormatPaymentMethod(e.PaymentMethod),
        ReferenceNumber = e.ReferenceNumber,
        CashSessionId = e.CashSessionId
    };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string FormatPaymentMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        PaymentMethod.BankTransfer => "Bank transfer",
        PaymentMethod.Cheque => "Cheque",
        PaymentMethod.Other => "Other",
        _ => method.ToString()
    };
}