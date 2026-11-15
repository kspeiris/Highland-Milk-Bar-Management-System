# Reporting & Export

Centralised reporting and export introduced in **Step 13**. One reusable pipeline (catalogue → dataset → preview → export) serves every report; the same `ReportResult` powers the preview and all export formats, so they always agree.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `DTOs/Reports/ReportDtos.cs` | `ReportResult`, `ReportColumn`, `ReportRow`, `ReportDefinition`, `ReportSummaryItem`, enums. |
| Application | `IReportCatalogService`, `IReportQueryService`, `IReportExportService` | Catalogue, dataset generation, export contracts. |
| Infrastructure | `Reports/ReportCatalogService` | Static catalogue of available reports. |
| Infrastructure | `Reports/ReportQueryService` | Builds datasets by reusing existing query services. |
| Infrastructure | `Reports/ReportExportService` + `SimplePdf` | CSV (native), Excel (ClosedXML), PDF (dependency-free). |
| WPF | `ReportCentreViewModel` / `ReportCentreView` | Report picker, period, generate, preview grid, export and print. |

## Report catalogue (by category)

- **Sales:** Summary, Transactions, by Product, by Category, Payment Method.
- **Products:** Performance, No-Sale.
- **Inventory:** Current Stock, Valuation, Low Stock, Expiry (current snapshots).
- **Purchases:** Supplier Outstanding (STEP 11), Purchases by Supplier.
- **Customers:** Directory, Top Customers by Spending (STEP 09 rules).
- **Expenses:** By Category (STEP 10 rules).
- **Financial:** Profit & Loss, Cash Flow, Supplier Payables, Payment Analysis — all from `IFinancialQueryService` (STEP 11), so they match the Finance workspace.
- **Milk Bar:** Recipe Costing (estimated, STEP 07).

## Data sources & reuse

Financial figures come from `IFinancialQueryService`; sales/product from `IBusinessAnalyticsQueryService`; inventory, customer, milk-bar, purchase and expense from their existing services. No business calculation is duplicated.

## Parameters & validation

Reports that require a period use the STEP 12 `ReportingPeriod` model (preset or custom From/To). Snapshot reports (inventory, customers, milk bar) ignore the period and label themselves "current snapshot". Invalid ranges fall back to the selected preset.

## Preview & printing

Preview binds a `DataTable` built from the report dataset. Printing uses a WPF `FlowDocument` via `PrintDialog` (no external dependency); large reports paginate automatically.

## Known limitations

- Inventory/customer/milk-bar reports are snapshots, not historical series.
- Recipe costing is an estimate (current recipe cost), not historical COGS.
- Printing is implemented but not verified on hardware in CI.

## Related docs

- `EXPORT_FORMAT_RULES.md` — PDF/Excel/CSV formatting and safety.