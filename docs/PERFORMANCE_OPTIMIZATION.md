# Performance & Optimization

Step 15 performance baseline and findings. The rule applied: **measure first, optimize only when a measurement justifies it.**

## Method

`PerformanceBaselineTests` generates a representative dataset in an isolated temporary SQLite database and times the most common read paths plus a backup cycle. Timings are written to test output; generous upper bounds act as regression guards.

## Baseline measurements

Environment: Windows 11, .NET 10, `Debug`, x64, isolated SQLite file.
Dataset: 2,000 products, 1,000 customers, 5,000 sales.

| Operation | Measured |
| --- | --- |
| Product search (paged 50) | ~110 ms |
| Inventory overview | ~42 ms |
| Financial summary (90 days, 5,000 sales) | ~53 ms |
| Backup create + verify | ~65 ms |

## Findings

- No bottleneck at the measured dataset size. All key paths complete in well under 100 ms (backup ~65 ms).
- The financial summary sums sales in memory (`AsNoTracking`); at 5,000 sales this is ~53 ms. Extrapolated to 50,000 sales it is still well under a second, so no schema or query change was justified.
- Read services already use `AsNoTracking`, database-side filtering/ordering and pagination, and the schema already defines indexes for the common filter columns (SKU, barcode, `SoldAt`+status, foreign keys, etc.).

## Optimizations applied

- **None.** No speculative optimization was introduced, because measurements did not show a problem. This preserves business-rule correctness and avoids unnecessary risk before deployment.

## Areas to re-measure on target hardware

- Application startup time (dominated by EF migration + seeding).
- Large exports (Excel/PDF) with tens of thousands of rows.
- POS search on a much larger product catalogue.

If any of these regresses on production hardware, re-run `PerformanceBaselineTests` and address the specific measured path (e.g., database-side aggregation for financial summaries).