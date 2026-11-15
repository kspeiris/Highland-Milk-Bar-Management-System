# Purchase Receiving Rules

How a purchase is received into inventory, atomically and exactly once. Implemented by `PurchaseService.ReceiveAsync`, which delegates batch/movement creation to the shared `IInventoryService`. There is **no separate purchase-stock system** — receiving through Purchase Management and manual stock-in both go through the same inventory engine.

## Preconditions

| ID | Rule | Enforcement |
| --- | --- | --- |
| PUR-11 | A purchase cannot be received twice | Rejected when `Status = Received` or `ReceivedAt` is set |
| PUR-15 | A cancelled purchase cannot be received | Rejected when `Status = Cancelled` |
| PUR-01 | A valid supplier is required | Rejected when `SupplierId` is null |
| PUR-02 | At least one item | Rejected when there are no items |

## Per-line validation (before any write)

| ID | Rule | Enforcement |
| --- | --- | --- |
| PUR-04 | Quantity > 0 | Rejected |
| PUR-05 | Unit cost ≥ 0 | Rejected |
| — | Product is inventory-tracked | Rejected (non-tracked items cannot be received) |
| PUR-06 | Batch number present | Rejected when empty |
| PUR-07 | Expiry present when `Product.RequiresExpiryDate` | Rejected |
| — | Expiry not before received date | Rejected when earlier than today |

`IInventoryService.ReceiveAsync` additionally enforces product-active and a unique `(ProductId, BatchNumber)` constraint.

## Receiving flow (atomic — PUR-18)

1. Load purchase with items and products; run all validations above.
2. Compute landed unit cost per line (charge allocation — see `PURCHASE_COST_CALCULATION.md`).
3. `BeginTransaction`.
4. For each line: `IInventoryService.ReceiveAsync(...)` → creates one `InventoryBatch`, one `StockMovement` (type `Purchase`), and increases `Product.CurrentStock`. Set `PurchaseItem.ReceivedQuantity`.
5. Set `Purchase.Status = Received`, `ReceivedAt = now`, `ReceivedDate = today`, and `PaymentStatus` from current `AmountPaid`.
6. `SaveChanges` + `Commit`.

If anything fails mid-way, the transaction rolls back **all** batch creation, stock movements, product-stock changes and the purchase status — nothing is partially committed.

## Effects

| Artefact | Result |
| --- | --- |
| `InventoryBatch` | One per purchase line, at landed unit cost |
| `StockMovement` | One `Purchase` movement per line |
| `Product.CurrentStock` | Increased by received quantity |
| `Purchase.Status` | `Received` |
| `Purchase.ReceivedAt` / `ReceivedDate` | Set |

## Idempotency across restarts

Because receiving sets `Status = Received` and `ReceivedAt`, a second attempt (repeated clicks or after an application restart) is rejected. Batch-number uniqueness per product is a second guard against duplicate batches.

## Relationship to manual stock-in (Step 05)

Manual stock-in (`StockInView` → `IInventoryService.ReceiveAsync`) remains available and unchanged. Supplier receiving uses the **same** `IInventoryService` — there is exactly one inventory engine.