# Expense Management

Recording and reviewing outlet operating expenses. Built in **Step 10** on the existing `Expense` / `ExpenseCategory` entities (no schema change).

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Domain | `Expense`, `ExpenseCategory` (existing) | Description, amount, date, category, payment method, reference/notes, optional cash session. |
| Application | `IExpenseService` | Expense CRUD + category management. |
| Application | `IExpenseQueryService` | Listing (DB-side filter/sort/paging), detail, stats, breakdown, trend, report. |
| Application | `ExpenseDtos.cs` | Filter/row/detail/create/update/category/stats/analytics/report DTOs. |
| Infrastructure | `ExpenseService` | EF Core implementation. |
| WPF | `ExpensesViewModel`, `ExpenseAnalyticsViewModel` | Listing + analytics. |
| WPF | `ExpensesView`, `ExpenseEditorWindow`, `ExpenseDetailsWindow`, `ExpenseCategoriesWindow`, `ExpenseAnalyticsWindow` | List, add/edit, view, category admin, analytics. |

No migration was required: `Expenses`, `ExpenseCategories`, `CashSessions` and `CashMovements` already exist from `InitialCreate`, and default categories are seeded by `SeedExpenseCategoriesAsync`.

## Features

- **Dashboard cards:** total expenses, this month, today, this-month count, category count.
- **Listing:** search by description/reference/category, filter by category/method/date range, sort by date/amount/category, pagination (15/page), all applied database-side.
- **Editor:** description, amount, date, category, payment method (Cash/Card/Bank transfer/Cheque/Other), reference (invoice/receipt no.), notes.
- **Categories:** add, rename, activate/deactivate (window).
- **Analytics:** monthly trend (6 months), category breakdown for a range, and a date-range report (total, count, average, breakdown).

## Expense lifecycle

1. Create (optionally linked to an open cash session).
2. Edit details (cash-session link is set at creation and preserved on edit).
3. Delete (removes the expense and any linked cash movement).

## Related docs

- `EXPENSE_BUSINESS_RULES.md` — validation and isolation rules.
- `EXPENSE_ANALYTICS.md` — metrics and calculations.