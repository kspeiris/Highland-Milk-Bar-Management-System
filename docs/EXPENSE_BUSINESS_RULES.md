# Expense Business Rules

Authoritative rules for expenses. Implemented in `ExpenseService`.

## Validation

| Field | Rule |
| --- | --- |
| Description | Required, trimmed; blank rejected. |
| Amount | Must be greater than zero. |
| Expense date | Stored as given (defaults to today in the UI). |
| Category | Optional; when supplied must reference an existing category. |
| Payment method | One of Cash, Card, Bank transfer, Cheque, Other. |
| Reference | Optional invoice/receipt number (max 120 chars). |
| Notes | Optional. |
| Cash session | Optional; when supplied must exist and be **Open**. |

## Category rules

- Category names are unique (case-insensitive).
- Categories can be deactivated but not deleted from the UI; existing expenses keep their category reference.
- Renaming a category updates its display everywhere via the stored foreign key.

## Cash integration

- When an expense is linked to an open cash session, a `CashMovement` (type `Expense`, direction `Out`) is recorded so the drawer reflects the payout.
- Deleting the expense removes the linked movement.
- Linking to a **closed** session is rejected.
- The cash link is fixed at creation; editing an expense does not move it between sessions.

## Isolation (important)

Recording, editing or deleting an expense **never** changes:

- sales revenue or sale records,
- supplier balances or purchase records,
- inventory quantities or batches.

An expense is a separate financial activity. (Verified by `Expense_DoesNotTouchSalesOrInventory`.)

## Out of scope for Step 10

- Recurring-expense definitions (documented as a future enhancement; no schema change is made here).
- File attachments — the `ReferenceNumber` field stores the invoice/receipt reference.
- Approval workflows and multi-currency.