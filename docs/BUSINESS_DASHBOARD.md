# Business Dashboard

Central business overview built in **Step 12**. Read-only; financial figures come from the Step 11 `IFinancialQueryService`, inventory/customer/milk-bar/purchase/expense summaries come from their existing query services, and only sales/product aggregates that no service exposes are queried directly.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `IBusinessAnalyticsQueryService` | Read-only orchestration across modules. |
| Application | `BusinessAnalyticsDtos.cs` | Overview, sales/product analytics, comparisons and alerts DTOs. |
| Application | `Common/ReportingPeriod.cs` | Shared reporting-period model (presets + previous equivalent window). |
| Infrastructure | `BusinessAnalyticsQueryService` | Composes services + DB-side sales/product aggregates. No writes. |
| WPF | `DashboardViewModel` / `DashboardView` | Business overview (KPIs, trend, top products, category split, inventory/customer insights, alerts). |
| WPF | `AnalyticsViewModel` / `AnalyticsView` | Tabbed analytics: Sales, Products, Inventory, Customers, Milk Bar, Purchases, Expenses. |

## Reporting periods

`ReportingPreset`: Today, Yesterday, This Week, Last Week, This Month, Last Month, This Year, Last Year (Monday-based weeks, local business dates, inclusive start / exclusive end). `ReportingPeriod.Previous()` yields the immediately preceding equal-length window for comparisons.

## Financial reuse (authoritative)

Net sales, gross profit, operating profit, operating expenses, cash flow, payment mix and payables come from `IFinancialQueryService`. The dashboard does **not** recalculate them.

## Comparison rules

`ComparisonDto` reports current, previous, difference and percent change. When the previous value is zero it returns `IsNewActivity` and a null percent (never Infinity/NaN); the UI shows "New activity" rather than a misleading 100%.

## Snapshot vs historical

Inventory and customer counts are **current snapshots**. Only sales/product figures are period-scoped. The dashboard never presents current stock as a historical value.

## Related docs

- `BUSINESS_ANALYTICS.md` — service architecture and metric sources.
- `BUSINESS_ALERT_RULES.md` — rule-based alerts.