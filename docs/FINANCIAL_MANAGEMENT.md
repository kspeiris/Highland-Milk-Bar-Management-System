# Financial Management

Read-only management reporting built in **Step 11** over existing sales, sales-return, supplier-payment and expense ledgers. **No schema change** and **no writes** to any operational ledger.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `IFinancialQueryService` | Read-only P&L, cash flow, payment mix, payables and reconciliation queries. |
| Application | `FinancialDtos.cs` | Summary, cash-flow line/summary, payment breakdown, supplier liability/ageing, cash reconciliation and monthly series DTOs. |
| Infrastructure | `FinancialQueryService` | EF Core queries; amounts loaded into memory and summed with .NET `decimal` (SQLite cannot aggregate decimals server-side). |
| WPF | `FinancialViewModel` | Period-driven workspace: loads all views for a selected range and builds P&L / cash-flow lines. |
| WPF | `FinancialView` | Tabbed workspace: Overview, Cash Flow, Cash Reconciliation, Supplier Payables, Payment Analysis. |

Wired into the shell at the existing **Finance** navigation entry (replacing the Step 11 placeholder). `FinancialQueryService` is constructed once in `App.xaml.cs` and passed to `MainViewModel`.

## Workspace

- **Period selector:** This month, Last month, Last 30 days, This year, Today, plus editable From/To dates and Apply/Refresh.
- **Overview:** KPI cards (net sales, gross profit, operating profit, operating expenses with margins and period-over-period change), a profit-and-loss table and a 12-month performance table.
- **Cash Flow:** cash in, cash out, net operating cash flow, with inflow/outflow breakdowns.
- **Cash Reconciliation:** open and recently closed sessions — opening float, cash in/out, expected vs counted closing and variance.
- **Supplier Payables:** outstanding, overdue and supplier count, ageing buckets and a per-supplier liability table.
- **Payment Analysis:** sales, supplier and expense payment-mix by method with amounts and percentages.

## Data sources

| Figure | Source |
| --- | --- |
| Gross sales, discounts, COGS | Non-voided `Sale` rows (`Subtotal`, `DiscountAmount`, `CostOfGoodsSold`). |
| Sales returns | Completed `SalesReturn` rows (`TotalAmount`). |
| Operating expenses | `Expense` rows in the period. |
| Cash sales | `Payment` rows with `Method = Cash` on non-voided sales. |
| Supplier payments | `SupplierPayment` rows (each is a real payment; `Purchase.AmountPaid` is never counted separately). |
| Payables | `Purchase` rows with `Status = Received` (`TotalAmount − AmountPaid`). |
| Cash reconciliation | `CashSession` + cash `Payment` (by `Sale.CashSessionId`), cash `Expense` (by `CashSessionId`) and `CashMovement` PayIn/Payout. |

## Related docs

- `FINANCIAL_BUSINESS_RULES.md` — formulas, rounding and read-only guarantees.