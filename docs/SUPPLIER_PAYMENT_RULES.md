# Supplier Payment Rules

How payments to suppliers are recorded, validated and reflected in balances. Implemented by `SupplierPaymentService` (`ISupplierPaymentService`).

## Principles

- A payment is recorded against a **received** purchase. Draft and cancelled purchases cannot be paid.
- Recording a payment changes **only** money and the settlement state. It never touches inventory or the receiving status.
- Payments are recorded outside the app (cash, bank transfer, cheque, other). No gateway integration.

## Payment methods

`Cash`, `BankTransfer`, `Cheque`, `Card`, `Other` (`PaymentMethod`). `Cheque` was added in Step 08 for supplier payments.

## Validation (PUR-13)

| Rule | Behaviour |
| --- | --- |
| Amount > 0 | Rejected with `ArgumentException` otherwise |
| Purchase must be received | Rejected otherwise |
| Amount ≤ outstanding | Rejected when it exceeds `TotalAmount − AmountPaid` (tolerance 0.005) |

The outstanding check uses the sum of all prior payments for the purchase, so multiple partial payments are supported.

## Outstanding and status

```text
Outstanding   = Received purchase TotalAmount − Σ allocated payments   (floored at 0)
PaymentStatus = Unpaid         if amount paid = 0
                PartiallyPaid  if 0 < amount paid < total
                Paid           if amount paid ≥ total
```

Computed by `PurchaseFinancials.Outstanding` and `PurchaseFinancials.ResolvePaymentStatus`, then persisted on the purchase (`AmountPaid`, `PaymentStatus`). A payment greater than the remaining balance is rejected before any write.

## Recording flow (transactional)

1. Load the purchase (with supplier) and validate amount, received status and outstanding.
2. Begin a transaction.
3. Insert the `SupplierPayment`.
4. Update `Purchase.AmountPaid` and `Purchase.PaymentStatus`.
5. Save and commit.

## What a payment never does

- Does **not** change product stock or batches.
- Does **not** change `Purchase.Status` (receiving lifecycle).
- Does **not** create inventory movements.

## Statement effect

A payment is a **credit** on the supplier statement (reduces the balance). See `SUPPLIER_STATEMENTS.md`.

## Out of scope (future)

- Unallocated supplier advances/credits.
- Purchase returns and supplier credit notes.
- Bank/gateway integration.