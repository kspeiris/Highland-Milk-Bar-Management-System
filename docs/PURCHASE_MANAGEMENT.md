# Purchase Management

Supplier invoices, purchase items, goods receiving, purchasing dashboard and purchase price history. Built in **Step 08** on top of the Step 05 inventory engine, the Step 04 product catalogue and the Step 03 design system.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `IPurchaseService` | Purchase lifecycle: create draft, edit, receive, cancel, dashboard. |
| Application | `IPurchaseQueryService` | Outstanding balances and purchase price history. |
| Application | `PurchaseDtos.cs` | Purchase/payment/dashboard/price-history DTOs. |
| Application | `PurchaseFinancials` | Pure cost/payment arithmetic (shared with tests and WPF). |
| Infrastructure | `PurchaseService` | EF Core implementation; receiving delegates to `IInventoryService`. |
| WPF | `PurchasesViewModel`, `PurchaseEditorViewModel`, `PurchaseDetailsViewModel` | Hub list, entry editor, details + payment. |
| WPF | `PurchasesView`, `PurchaseEditorWindow`, `PurchaseDetailsWindow` | Dashboard, entry grid, receiving/payment. |

No new tables beyond the Step 08 columns. See `PURCHASE_RECEIVING_RULES.md` and `PURCHASE_COST_CALCULATION.md`.

## Purchase model

A purchase is a **supplier invoice and its full goods receipt** (one receipt for the whole invoice). Advanced multi-delivery purchase orders are out of scope for this phase.

- **Header** (`Purchase`): internal `PurchaseNumber` (e.g. `PUR-00000001`), optional supplier `InvoiceNumber`, `SupplierId`, `PurchaseDate`, `DueDate`, `Subtotal`, `DiscountAmount`, `AdditionalCharges`, `TotalAmount`, `AmountPaid`, `Status`, `PaymentStatus`, `ReceivedDate`/`ReceivedAt`, `Notes`.
- **Line** (`PurchaseItem`): `ProductId`, `Quantity`, `Unit` (snapshot), `UnitCost`, `DiscountAmount`, `LineTotal`, `BatchNumber`, `ManufacturedDate`, `ExpiryDate`, `ReceivedQuantity`.

Receiving status (`PurchaseStatus`: Draft / Received / Cancelled) is kept **separate** from settlement status (`PaymentStatus`: Unpaid / PartiallyPaid / Paid).

## Lifecycle

```text
DRAFT --receive--> RECEIVED --payment(s)--> UNPAID / PARTIALLY PAID / PAID
  |
  +--cancel--> CANCELLED
```

- **Draft** — editable. Does **not** touch stock (PUR-08).
- **Receive** — validates every line, creates inventory batches and stock movements, sets `Status = Received`. Transactional; cannot be received twice (see receiving rules).
- **Cancel** — only for drafts. Received purchases cannot be cancelled (they already affected inventory); use a return/credit note (future).

## Screens

- **Purchase dashboard** — six KPIs (received purchases, purchase value, active suppliers, outstanding payments, payments this month, purchases this month), six-month monthly trend, and the recent received purchases. Values come from the database only.
- **Purchase list** — search, supplier/receiving/payment filters, paging, and row actions (edit, receive, cancel, details).
- **Purchase entry** — supplier + invoice header, an editable product line grid, and a live financial summary (subtotal, discount, additional charges, grand total). Save as draft or save & receive.
- **Purchase details** — items, financial summary, payment history, and a record-payment panel; receive action when still a draft.

## Cost calculation

See `PURCHASE_COST_CALCULATION.md`. Line total = qty × unit cost − line discount; subtotal = Σ line totals; grand total = subtotal − purchase discount + additional charges. On receiving, additional charges/discounts are spread across lines proportionally so batch unit cost is the **landed cost**.

## Purchase price history

`IPurchaseQueryService.GetPriceHistoryAsync(productId, supplierId?)` returns every **received** purchase line for a product with date, supplier, invoice, quantity, unit and unit cost, plus latest/lowest/highest. Historical costs are **never overwritten** when new purchases arrive (PUR-14). Open it from the purchase workspace price-history entry point; the same data backs product-level purchase history.

## Business rules

See `PURCHASE_RECEIVING_RULES.md` (PUR-01…PUR-18). Highlights: valid active supplier (01/03), ≥1 item (02), positive quantity (04), non-negative cost (05), batch required (06), expiry required when tracked (07), drafts never affect stock (08), receive creates batches (09) and movements (10), no double receive (11), unique invoice per supplier (12), no overpayment (13), price history preserved (14), cancelled purchases affect nothing (15), received purchases are never deleted (16), balances exclude drafts/cancelled (17), receiving is transactional (18).