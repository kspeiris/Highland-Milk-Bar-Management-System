# Supplier Statements

The supplier ledger: a dated, running-balance statement of what a supplier is owed and what has been paid. Implemented by `SupplierService.GetStatementAsync`.

## Debit / credit convention

| Entry | Debit | Credit | Effect |
| --- | --- | --- | --- |
| Received purchase | `TotalAmount` | — | Increases what the supplier is owed |
| Supplier payment | — | `Amount` | Reduces what the supplier is owed |

`RunningBalance = previous balance + Debit − Credit`. A positive balance means the outlet owes the supplier.

## What counts

- **Received** purchases only — drafts and cancelled purchases are excluded (PUR-17).
- Allocated payments only — each payment reduces the balance.
- Only committed records — never draft/duplicate data.

## Period and opening balance

`GetStatementAsync(supplierId, from?, to?)`:

- `from` defaults to 100 years ago; `to` defaults to today.
- **Opening balance** = Σ received purchases dated before `from` − Σ payments before `from`.
- Statement lines are the received purchases and payments dated within `[from, to]`.
- **Closing balance** = opening + Σ(debit) − Σ(credit) over the period.

## Line fields

| Column | Meaning |
| --- | --- |
| Date | Business date (received date for purchases, paid date for payments) |
| Reference | Purchase number or payment reference |
| Type | `Purchase` or `Payment` |
| Description | Human-readable description (includes supplier invoice when present) |
| Debit / Credit | Amount owed / amount paid |
| Running Balance | Balance after this line |

Lines are ordered by date, with payments after purchases on the same date (purchase debits before its payment credit).

## Related screens

- **Supplier details** — overview (total purchased, total paid, outstanding), purchase history, payment history, and the statement grid with opening/closing balances.
- **Outstanding balances** — per-purchase balances (`IPurchaseQueryService.GetOutstandingAsync`), optionally filtered to overdue, computed from received purchases only.

## Out of scope (future)

- Purchase returns and supplier credit notes (would add credit entries).
- Unallocated supplier advances.