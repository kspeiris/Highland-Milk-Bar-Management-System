# POS Customer Integration

Optional customer selection during checkout. Implemented in `PosViewModel` / `PosView` (UI) and `SalesService.CheckoutAsync` (validation and persistence). The existing 70:30 POS layout and the single `SalesService` checkout engine are preserved.

## UI

- A **CUSTOMER** panel sits at the top of the right-hand order column.
- Search customers by name, phone or code; the list shows active customers only.
- Selecting a customer shows name and phone with **Change** (search again) and **Remove**.
- With no customer selected the panel reads **Walk-in Customer**.
- Removing the customer returns the sale to walk-in checkout.

## Checkout behaviour

| Rule | Behaviour |
| --- | --- |
| Optional | Customer selection is never required; walk-in checkout is unchanged. |
| Association | The selected `CustomerId` is written to the sale as part of the existing atomic checkout transaction. |
| Inactive customers | Cannot be selected; if an inactive id reaches the service the checkout is rejected. |
| Invalid ids | A `CustomerId` that does not exist is rejected before any write. |
| History | Existing sales are never modified; the customer link does not rewrite amounts, payments or inventory. |
| Loyalty | Points are awarded inside the same transaction for customer sales (see `CUSTOMER_LOYALTY_RULES.md`). |

## Single engine

- `SalesService` remains the only checkout implementation. No second checkout service or duplicate billing path was introduced.
- Inventory allocation, recipe deduction, COGS, payment calculation and receipt generation are unchanged; the only additions are customer validation and the loyalty ledger entry, both inside the existing transaction.

## Persistence

- `Sale.CustomerId` (nullable) stores the association; `Customer` is the existing entity.
- `SearchCustomersAsync` (on `ISalesQueryService`) supplies the picker and filters to active customers.

## Verification

- Tests in `SalesCustomerIntegrationTests` cover: customer sale persists the link and awards loyalty, walk-in sale stores no customer and earns nothing, inactive and invalid customers are rejected, and inventory is still deducted.