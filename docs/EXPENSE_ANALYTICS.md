# Expense Analytics

Read-only analytics derived from the `Expenses` table. Implemented in `ExpenseService`; displayed in `ExpenseAnalyticsWindow`.

## Metrics

| Metric | Calculation |
| --- | --- |
| Total expenses | Sum of `Amount` over all expenses. |
| This month | Sum of `Amount` where `ExpenseDate` is on/after the first of the current month. |
| Today | Sum of `Amount` where `ExpenseDate` is today. |
| This-month count | Number of expenses this month. |
| Category count | Number of configured expense categories. |

## Category breakdown

For an optional date range (`From`/`To` inclusive):

- Group expenses by category (uncategorised expenses appear under **Uncategorized**).
- Report total amount, expense count and share percentage per category (`total ÷ grand total × 100`, rounded to 1 dp).
- Ordered by total amount descending.

## Monthly trend

- Last N months (default 6, capped at 24) as `yyyy-MM` rows.
- Each row: month label, expense count and total amount.
- Months with no expenses appear with zero totals.

## Date-range report

For a chosen `From`/`To` (auto-swapped if reversed):

- Total amount, expense count, average amount.
- Category breakdown for the same range.

## Notes

- All aggregations run in the database (`Sum`/`Count`/`GroupBy`); the trend materialises only month/amount pairs.
- `To` is treated inclusively (the query uses `< To + 1 day`).