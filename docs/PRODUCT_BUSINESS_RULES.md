# Product Business Rules

How each Step 04 business rule (PR-01…PR-12) is enforced, and where. Rules are enforced in the Application/Infrastructure services; the database provides backing constraints where they already exist.

| ID | Rule | Enforcement |
| --- | --- | --- |
| PR-01 | Every product must have a name. | `ProductService.CreateAsync` / `UpdateAsync` reject empty names with `ArgumentException`. |
| PR-02 | Every product must belong to a valid category. | Create/Update verify the category exists; create also requires it to be **active**. |
| PR-03 | SKU must be unique. | Service check (create and update) **and** the existing unique index `IX_Products_Sku`. |
| PR-04 | Barcode must be unique when provided. | Service check: empty/null barcodes are allowed (multiple nulls), duplicates are rejected. Backed by a non-unique `IX_Products_Barcode` index — a filtered unique index is a recommended future enhancement. |
| PR-05 | Selling price cannot be negative. | `CreateAsync` / `UpdateAsync` reject `SellingPrice < 0`. |
| PR-06 | Purchase price cannot be negative. | `CreateAsync` / `UpdateAsync` reject `CostPrice < 0`. |
| PR-07 | Inactive products cannot be added to new sales. | Enforced in the sales/POS workflow (Step 06); product deactivation excludes them from selection. |
| PR-08 | Products referenced by transactions cannot be hard-deleted. | `ProductService.DeleteAsync` checks sale items, purchase items, stock movements, recipe items, recipes, inventory batches and stock adjustments; throws when referenced. The UI uses deactivate instead. |
| PR-09 | Inventory-tracked products must have a valid measurement unit. | Create/Update require a unit when `TrackInventory` is true. Units are loaded from the `Units` table. |
| PR-10 | Existing transactions must preserve historical prices. | Product edits never rewrite `SaleItem.UnitPrice`/`UnitCostSnapshot` or `PurchaseItem.UnitCost`; editing a product only changes the catalogue record. |
| PR-11 | Products can belong to different product types. | The `ProductType` enum (`Packaged`, `Ingredient`, `PreparedItem`) is used as-is; no duplicate enum. |
| PR-12 | Deactivating a category must not silently deactivate its products. | `CategoryService.SetActiveAsync` changes only `Category.IsActive` and never touches products. |

## Stock and pricing integrity

- Creating a product does **not** create stock movements. Opening stock is stored directly on `CurrentStock` only when the product is created with `TrackInventory` and an opening quantity.
- Editing a product never changes `CurrentStock` and never inserts stock movements.
- Historical transaction prices (sales and purchases) are snapshot fields and are never updated by product edits.

## Result and error contract

Services return DTOs and throw with clear messages:

- `ArgumentException` for validation failures (missing fields, duplicates, negative values, inactive category, missing unit).
- `InvalidOperationException` for state problems (product not found, delete blocked by references).
- `DbUpdateException` from a unique-constraint race is translated to `ArgumentException` with a friendly message.

The UI surfaces these messages through message boxes and the status line; buttons are disabled or guarded rather than silently doing nothing.