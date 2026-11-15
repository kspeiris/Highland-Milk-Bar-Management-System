# Inventory Business Rules

How the Step 05 inventory rules and the database invariants (DBR-02…DBR-05, INV-01) are enforced, and where. Rules are enforced in the Application/Infrastructure services; the database provides backing constraints.

| ID | Rule | Enforcement |
| --- | --- | --- |
| DBR-02 | Every stock-tracked product must have an inventory record. | `InventoryService.ReceiveAsync` creates the batch and movement; batch queries are the source of truth for tracked products. |
| DBR-03 | Expired products cannot be sold. | `InventoryService.AllocateAsync` filters out batches with `ExpiryDate < today`; the allocation never consumes expired stock. |
| DBR-04 | Negative stock is not allowed by default. | `AdjustAsync` / `AllocateAsync` reject movements that would drive a batch (or product) negative. |
| DBR-05 | FIFO/FEFO must be supported. | `AllocationPolicy` enum + `AllocateAsync` ordering by received date (FIFO) or expiry date (FEFO). |
| INV-01 | `Product.CurrentStock` equals the sum of non-expired batch balances. | Maintained by `InventoryService` on receipt/adjustment/allocation; reads still aggregate over batches. |

## Receiving (stock in)

- A receipt requires an **active, inventory-tracked** product.
- **Batch number must be unique per product** (case-insensitive); duplicates are rejected.
- Quantity must be **greater than zero**; unit cost **zero or more**.
- If `Product.RequiresExpiryDate` is true, an **expiry date is required**; otherwise it is optional.
- The expiry date **cannot be before** the received date.
- A receipt creates an `InventoryBatch`, a stock movement (Opening stock / Manual receipt / Positive correction) and updates `Product.CurrentStock`.

## Adjustments

- An adjustment line targets a product (and optionally one batch) with a non-zero quantity change.
- **Damaged** and **Expired** adjustments always reduce stock regardless of the sign entered.
- **Correction** adjustments follow the entered sign (positive increases, negative decreases).
- Every adjustment writes a stock movement (AdjustmentIn / AdjustmentOut / Waste / Expired) and refreshes derived stock, so the ledger stays complete.

## Expiry

- Expiry is **date-only**: a batch is expired when `ExpiryDate.Date < today`; it is still sellable on its expiry date.
- *Sellable* stock excludes expired batches; *physical* stock includes them.
- Near-expiry list = batches expiring within **30 days**; expired list = batches with `ExpiryDate` in the past.
- Expired batches remain on the books (as physical stock) until written off via an Expired adjustment.

## Ledger integrity

- Every quantity change (receipt, sale consumption, adjustment, write-off) produces a `StockMovement` row — the ledger is append-only and never edited.
- Consumption records `StockAllocation` rows tying each sale line to the batches consumed, so FIFO/FEFO decisions are auditable.

## Money and rounding

- Quantities: `decimal(18,3)`. Unit costs: `decimal(18,4)`. Values: `decimal(18,2)`. All calculations use `decimal`.