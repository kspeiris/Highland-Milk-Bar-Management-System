# Product Management

Module for creating, viewing, editing, searching, filtering and managing Highland Milk Bar products. Built in **Step 04** on top of the existing SQLite database and the Step 03 design system.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `IProductService` | Product use cases (search, CRUD, activation, delete guard). |
| Application | `ProductDtos.cs` | `ProductDto`, `ProductDetailDto`, `CreateProductRequest`, `UpdateProductRequest`, `ProductFilter`, `PagedResult<T>`, `ProductStatsDto`, `UnitDto`. |
| Infrastructure | `ProductService` | EF Core implementation, validation, database-backed filtering/paging. |
| WPF | `ProductsViewModel` | Filters, paging, stats, CRUD orchestration. |
| WPF | `ProductsView` + `ProductEditorWindow` + `ProductDetailsWindow` | Listing, add/edit form, read-only details. |

No new tables or `DbContext` were introduced. The existing `Product` entity is reused unchanged.

## Product listing

Columns: Product, SKU, Barcode, Category, Type, Cost, Sell, Stock, Status.

Features:
- **Search** by name, SKU, barcode or category (database-side `LIKE`).
- **Filter** by category, product type and status (active/inactive).
- **Sort** by clicking any column header (name, SKU, category, cost, sell, stock).
- **Paging** (15 per page) with next/previous and a "Page X of Y • N products" label. All filtering, sorting and paging run in the database, not in memory.
- **Clear** resets all filters.
- **Refresh** reloads data and counters.

Above the grid: stat cards for total products, active, inactive and category count.

## Add / Edit product form

`ProductEditorWindow` presents three sections:

1. **General information** — name, SKU, barcode, category, product type.
2. **Pricing information (LKR)** — purchase price, selling price.
3. **Inventory settings** — unit (loaded from the `Units` table), reorder level, "track inventory", "active".

The same window is used for add and edit (title/buttons switch). Validation is shown inline in a red error line; the dialog stays open until the input is valid. Stock is **not** editable here — opening stock is set on create only and later changes go through inventory.

## Product details

`ProductDetailsWindow` shows a read-only view: general information, pricing, inventory configuration, status and timestamps.

## Activation

Products are **activated / deactivated**, never hard-deleted from the UI. Inactive products are excluded from the POS product selection (enforced in the POS workflow) and can be filtered in this screen.

A hard delete API (`DeleteAsync`) exists for administrivia but throws when the product is referenced by sales, purchases, stock movements, batches, recipes or adjustments (PR-08).

## Units

Units are loaded from the `Units` table (`GetUnitsAsync`). The selected unit name is stored on the product; for inventory-tracked products the selected unit is also set as the product's base unit on creation. No unit list is hard-coded in the view model.

## Product types

The existing `ProductType` enum is used — no duplicate enum was created. The UI labels map to the business concepts as follows:

| Business concept | `ProductType` value | UI label |
| --- | --- | --- |
| Packaged product | `Packaged` | Packaged |
| Ingredient | `Ingredient` | Ingredient |
| Milk Bar menu item / prepared beverage / ice cream product | `PreparedItem` | Prepared item |

Recipe-based stock deduction is **not** triggered by this module; it belongs to the Milk Bar/POS workflow.

## Known limitations

- The `Product` entity has no **description** or **minimum selling price** column, so those fields from the brief are intentionally omitted (the brief allows optional fields "only if supported by the existing model").
- Barcode uniqueness is enforced at the service layer. The database has a non-unique `IX_Products_Barcode` index; a filtered unique index is a recommended future enhancement (requires a migration).
- `Category` has no `CreatedAt` column, so category creation dates are not displayed.

## Testing

`ProductServiceTests` (real SQLite): create valid, missing name, missing category, duplicate SKU, duplicate barcode, null barcodes allowed, negative prices, negative reorder, track-without-unit, inactive category, update preserves stock, activate/deactivate, delete guard (referenced vs unreferenced), search by text/category/type/status, paging, stats.