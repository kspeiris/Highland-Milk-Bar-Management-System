# Database Business Rules — Highland Milk Bar Management System

These rules are enforced by the domain model, EF Core configuration, or the service layer.
"Enforced by" names the component that guarantees the rule.

| ID | Rule | Enforced by |
|---|---|---|
| DBR-01 | Every product must belong to a category. | `Product.CategoryId` (required FK, `Restrict`); validated in `ProductService.CreateAsync`. |
| DBR-02 | Every stock-tracked product must have an inventory record. | `InventoryService.ReceiveAsync` creates an `InventoryBatch`; `Product.TrackInventory` distinguishes tracked products. |
| DBR-03 | Expired products cannot be sold. | `InventoryService.AllocateAsync` filters `ExpiryDate >= today`; tests `ExpiredBatch_IsNotAllocated`. |
| DBR-04 | Negative stock is not allowed by default. | `AllocateAsync`/`AdjustAsync` throw on negative product/batch balances; `AppSettings["inventory.allow_negative_stock"] = false`. |
| DBR-05 | FIFO/FEFO must be supported. | `AllocationPolicy` enum + `InventoryService.AllocateAsync` ordering; tests `FifoAllocation_*` and `FefoAllocation_*`. |
| DBR-06 | Every sale must have at least one sale item. | `SalesService.CheckoutAsync` rejects empty item lists. |
| DBR-07 | Every completed sale must have valid payment records. | `CheckoutAsync` requires ≥1 payment and tender == total (within 0.001). |
| DBR-08 | Every stock movement must be recorded. | All inventory changes go through `InventoryService`, which writes `StockMovement` rows. |
| DBR-09 | Recipe sales must deduct ingredient quantities. | `SalesService.ConsumeRecipeAsync` allocates ingredients; test `RecipeConsumption_DeductsIngredientStock`. |
| DBR-10 | Purchase records must update inventory. | `PurchaseService.ReceiveAsync` creates batches + movements; drafts do not (status check). |
| DBR-11 | Returned products must update inventory according to their condition. | `SalesService.ProcessReturnAsync` restocks only `ReturnCondition.Restock` items; `Discard` does not. |
| DBR-12 | Historical sales prices must not change when product prices change. | `SaleItem.UnitPrice` / `ItemNameSnapshot` are snapshots captured at sale time. |
| DBR-13 | Historical recipe costs must be preserved. | `SaleItem.UnitCostSnapshot` + `SaleItem.RecipeId` capture the recipe used. |
| DBR-14 | Completed transactions must not be silently deleted. | `Sale`/`Payment` use `Restrict` delete; returns/voids via status. No hard delete in services. |
| DBR-15 | All financial calculations must be traceable. | Sale `Subtotal/Discount/COGS/AmountPaid` derive from lines, payments and cost snapshots; `StockMovement` + `StockAllocation` provide an audit trail. |

## Additional invariants

| ID | Invariant |
|---|---|
| INV-01 | `Product.CurrentStock` equals the sum of its non-expired batch balances for tracked products (maintained by the service layer). |
| INV-02 | A checkout is a single transaction: any failure rolls back sale, items, payments, stock, allocations and movements. |
| INV-03 | Split payments must sum exactly to the sale total before commit. |
| INV-04 | A sale cannot be returned beyond the quantity already sold (net of prior returns). |
| INV-05 | Unique keys (`Products.Sku`, `Sales.ReceiptNumber`, `SalesReturns.ReturnNumber`, `Categories.Name`, `AppSettings.Key`) are enforced by the database. |

## Money and rounding

- Monetary values: `decimal(18,2)`.
- Quantities: `decimal(18,3)`.
- Unit costs: `decimal(18,4)`.
- Calculations use `decimal` (no floating point). Revenue is never treated as profit; COGS
  comes from cost snapshots.