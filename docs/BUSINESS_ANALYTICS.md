# Business Analytics

Read-only analytics layer introduced in **Step 12**. It orchestrates existing services and adds only the aggregates no single service exposes. No transaction, financial, inventory or costing engine is duplicated.

## Service

`IBusinessAnalyticsQueryService` (Infrastructure `BusinessAnalyticsQueryService`):

| Method | Reuses | Notes |
| --- | --- | --- |
| `GetOverviewAsync` | `IFinancialQueryService`, `ICustomerQueryService`, `IInventoryQueryService` | KPIs, comparisons, top products, category split, trend, alerts. |
| `GetSalesAnalyticsAsync` | `IFinancialQueryService` (net/gross, payment mix) | Orders, AOV, units, trend, category, weekday, best day (DB-side sales aggregates). |
| `GetProductAnalyticsAsync` | — | Top sellers, high-profit, low performers, no-sale (DB-side sale-item aggregates). |
| `GetInventoryAnalyticsAsync` | `IInventoryQueryService` | Stock overview, low-stock/expiry alerts. |
| `GetCustomerAnalyticsAsync` | `ICustomerQueryService` | Segments, top customers, active/new/repeat (STEP 09 rules). |
| `GetMilkBarAnalyticsAsync` | `IMilkBarService` | Menu performance, margins, ingredient alerts (STEP 07 rules). |
| `GetPurchaseAnalyticsAsync` | `IPurchaseService` | Received value, suppliers, top purchased, payables. |
| `GetExpenseAnalyticsAsync` | `IExpenseQueryService` | Totals, category breakdown (STEP 10 rules). |
| `GetBusinessAlertsAsync` | all of the above | Rule-based alerts. |

## Sales/product aggregates

Status rules mirror the sales module: **non-voided** sales only; returns use **completed** `SalesReturn`; product profitability uses the historical `SaleItem.UnitCostSnapshot` (never the current product price). Amounts are loaded and summed in .NET `decimal` because SQLite cannot aggregate decimals server-side.

## Performance

`AsNoTracking` throughout, asynchronous with `CancellationToken`, DB-side filtering (no full-table loads), bounded ranking (top 10, no-sale capped at 50), and per-section error isolation in the UI (`AnalyticsViewModel.Guard`). A refresh never writes.

## Known limitations

- Sales trend switches to monthly buckets for windows longer than 31 days.
- Customer and inventory figures are snapshots, not historical series.
- No charting library is installed; the UI uses KPI cards, progress bars and data grids from the existing design system.