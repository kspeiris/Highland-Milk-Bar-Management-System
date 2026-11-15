# Sales Return Rules

Sales returns let staff accept goods back against an original sale. Returns are **additive and traceable** — the original sale is never modified, deleted, or reversed.

## Principles

- Every return references an original `Sale` (`SalesReturn.SaleId`).
- The original sale remains valid; returns are recorded separately.
- Returned quantity cannot exceed the sold quantity (minus any already returned) for a given item.
- Only a valid return reason and a valid condition may be recorded.
- Restocking is conditional: only `ReturnCondition.Restock` re-enters inventory.

## Return conditions (`ReturnCondition`)

| Condition | Re-enters stock? | Typical use |
| --- | --- | --- |
| **Restock** | Yes — sellable stock increased. | Unopened, resalable goods. |
| **Damaged** | No | Damaged in store or by customer. |
| **Expired** | No | Returned because expired. |
| **NotForResale** | No | Opened/unusable but kept for records. |
| **Discard** | No | Sent to waste. |

Restocking uses the original item's batch where possible and the sale's cost snapshot; it does not alter historical COGS.

## Process (UI)

1. In **Sales History** select a completed sale → **Process return**.
2. For each line, enter the returned quantity (≤ remaining) and choose a condition.
3. Set the refund method (cash / card / other) and a reason.
4. Submit. `SalesService.ProcessReturnAsync` validates and, within one transaction:
   - increments returned quantities (rejecting over-return),
   - records `SalesReturnItem` rows,
   - restocks only `Restock` items (via `InventoryService`), recording a movement,
   - marks the return complete.

Any validation failure rolls back the whole return.

## Refunds

- A return records a **refund method**; an external refund (e.g. to a card) is assumed to have been processed by the outlet.
- The return does **not** delete or edit the original sale or its payments.

## Guardrails

- Returns are only offered for completed sales.
- Over-returning is rejected.
- A return with no lines is rejected.
- All quantity checks use the sold quantity minus previously returned quantity.