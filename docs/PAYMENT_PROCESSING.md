# Payment Processing

How the POS records payments. Payments are recorded with the existing `Payment` entity (`Method`, `Amount`, `PaidAt`, `Reference`). No card gateway or bank integration is performed — card payments are recorded as externally processed.

## Payment methods

| Method | Behaviour |
| --- | --- |
| **Cash** | Record the cash tendered. If cash exceeds the amount due, `Change = cash − amountDue`. |
| **Card** | Record the full amount due as card. No cash change is generated. |
| **Split** | Record a cash entry and a card entry. Cash covers the remainder after card. |
| **BankTransfer / Other** | Recorded like card (non-cash); no change. |

## Validation (POS-08/09/10)

- At least one payment; each amount > 0.
- Non-cash tenders **cannot exceed** the sale total.
- Total tendered must be **≥** the total due (insufficient payment is rejected).
- `Change = AmountPaid − Total`, clamped to ≥ 0.

## Cash example

```
Total due : LKR 1,160
Cash given: LKR 1,500
Change    : LKR 340
```

## Split example

```
Total due : LKR 1,160
Cash      : LKR 500
Card      : LKR 660
Total     : LKR 1,160   (change 0)
```

## Recording

Payments are written inside the checkout transaction together with the sale and stock movements, so a completed sale always has its payment records. The receipt prints each payment method and amount, plus cash received and change.

## Notes

- The outlet's card terminal is external; the system records the card amount and an optional reference.
- Refund method for returns is recorded on the return (see `SALES_RETURN_RULES.md`); an external refund is not assumed to have completed.