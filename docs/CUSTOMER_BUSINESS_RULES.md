# Customer Business Rules

Authoritative rules for customer records. Implemented in `CustomerService` (commands) and `CustomerQueryService` (reads).

## Registration and editing

| Rule | Behaviour |
| --- | --- |
| Customer code | Generated `CUS-00000001` and never changed; unique across all customers. |
| Name | Required, trimmed; blank rejected. |
| Phone | Required on create. Stored normalized (digits and `+` only). Blank rejected on create. |
| Alternative phone | Optional; normalized the same way. |
| Email | Optional; when present must contain a single `@` with a dotted domain. |
| City / Address / DOB / Notes | Optional. |
| Active | New customers are active by default. |

## Duplicate phone

- `PhoneExistsAsync` reports whether another customer already uses a normalized number.
- A shared number is a **warning**, not a hard block: the editor surfaces it and the user decides. Legitimate shared numbers (e.g. a family or business line) remain allowed.

## Status

- Deactivating a customer never deletes them, never removes their sales and never changes historical amounts.
- Inactive customers cannot be selected for a new POS sale (enforced in `SalesService`).
- Reactivation is always allowed from the customer list.

## Deletion guard

A customer is deleted only when it has **no** sales, returns or loyalty ledger rows. Otherwise the operation throws and the customer must be deactivated instead.

## Completed sales definition

For history, spending and analytics a "completed sale" is any sale whose `Status` is not `Voided`. `PartiallyReturned` and `Returned` sales still count as purchases; the returned value is tracked separately and is not netted out of spending.

## Related docs

- `CUSTOMER_MANAGEMENT.md` — screens and architecture.
- `CUSTOMER_ANALYTICS.md` — how spending and segmentation are derived.
- `POS_CUSTOMER_INTEGRATION.md` — checkout-time validation.