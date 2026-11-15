# Inventory Management

Batch-level stock, receipts, adjustments, expiry tracking, FIFO/FEFO allocation and the stock movement ledger. Built in **Step 05** on top of the Step 01 database, the Step 04 product catalogue and the Step 03 design system.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `IInventoryService` | Commands that mutate stock: receive, allocate (FIFO/FEFO), adjust. |
| Application | `IInventoryQueryService` | Read-only queries backing the screens (overview, stock, batches, movements, expiry, low-stock). |
| Application | `InventoryDtos.cs`, `InventoryQueryDtos.cs` | Receipt/adjustment/allocation DTOs; query DTOs and `ExpiryRules`. |
| Infrastructure | `InventoryService`, `InventoryQueryService` | EF Core implementations; all reads aggregate over batches. |
| WPF | `InventoryViewModel` + child view models | One hub VM with a child VM per tab. |
| WPF | `InventoryView` + tab views | Overview, Stock, Batches, Stock In, Adjustments, Movements, Expiry. |

No new tables were introduced in Step 05. The schema additions are a single `Product.RequiresExpiryDate` column (migration `20261114190000_AddProductRequiresExpiryDate`).

## Source of truth

- **`Product.CurrentStock` is a cache.** Every stock query (overview, stock, batches, low-stock) derives quantities and value from **inventory batches**, never from `CurrentStock`.
- **Physical vs sellable.** *Physical* stock includes expired batches. *Sellable* stock excludes batches whose `ExpiryDate` is before today. The overview shows both quantity and value for each.
- Expiry uses **date-only** comparison: a batch expiring today is still sellable; it becomes expired the following day (`ExpiryRules`).

## Screens

The Inventory workspace is a single page with seven tabs.

### Overview
Headline KPIs (tracked products, low-stock count, batches expiring within 30 days, expired batches), valuation cards (physical vs sellable quantity and value), stock-by-category and top-stocked breakdowns, plus low-stock and expiry alert lists.

### Stock
Per-product stock grid. Columns: product, SKU, category, physical, sellable, expired, reorder level, shortfall and a colour-coded status (In stock / Low stock / Out of stock). Filters: search, category, status. Database-side paging (20 per page).

### Batches
Every batch with product, batch number, received date, manufactured date, expiry, received qty, remaining qty, unit cost, value, days remaining and expiry status. Filters: search, product, expiry bucket (all / near expiry / expired). Database-side paging.

### Stock In
Records a receipt that creates a new batch: product, batch number, received date, expiry date, quantity, unit (from the product), unit cost, receipt reason (Opening stock / Manual receipt / Positive correction), reference number and note. Validation: active tracked product, unique batch number per product, positive quantity, non-negative cost, expiry required when the product requires it, and expiry not before the received date.

### Adjustments
Applies a manual adjustment to a product (and optionally a specific batch): type (Correction ±, Damaged, Expired), quantity change, unit cost, reference and note. Damaged and expired always reduce stock. Each adjustment is written to the movement ledger and the derived tabs refresh automatically.

### Movements
The immutable stock movement ledger: date, product, SKU, batch, type, quantity change, balance after, unit cost, reference and note. Filters: search, product, movement type and date range. Database-side paging.

### Expiry
Two lists — batches expiring within 30 days and already-expired batches — with product, batch, expiry, quantity, value, days left and status.

## Allocation (FIFO / FEFO)

`InventoryService.AllocateAsync` consumes stock across batches under the requested `AllocationPolicy` (FIFO or FEFO), **skipping expired batches**. It records durable `StockAllocation` rows and stock movements so consumption is auditable. The POS and Milk Bar workflows consume stock through this method.

## Physical vs sellable valuation

- **Physical value** = Σ (all batch remaining qty × unit cost).
- **Sellable value** = Σ (non-expired batch remaining qty × unit cost).

Both are shown so that write-offs of expired stock are visible without hiding total holdings.

## Known limitations

- Product type stays at three values (`Packaged`, `Ingredient`, `PreparedItem`); section-5 concepts (prepared items) map to `PreparedItem`.
- Barcode uniqueness remains service-enforced (see Product Management).
- Batch-level value is cost-based; no landed-cost apportionment beyond the recorded unit cost.