# Customer Loyalty Rules

A points foundation only: earning and reversal, with an append-only ledger. Redemption and discounts are **not** part of Step 09. Implemented in `LoyaltyService` and `LoyaltyCalculator`; awarded/reversed from `SalesService` inside the sale/return transaction.

## Configuration

Stored in `AppSettings` (key/value), editable from the analytics screen.

| Key | Default | Meaning |
| --- | --- | --- |
| `loyalty.enabled` | `true` | Master switch; when false no points are awarded. |
| `loyalty.points_per_hundred` | `1` | Points per LKR 100 of qualifying spend. |
| `loyalty.minimum_qualifying_amount` | `500` | Minimum sale total (LKR) to earn points. |

## Earning

- Only completed sales with a customer earn points. Walk-in sales (no customer) never earn points.
- Points = `floor(saleTotal / 100 × pointsPerHundred)`.
- A sale below the minimum qualifying amount earns nothing.
- Rounding is always **down** (floor) to whole points; balances are integers.
- Example: sale LKR 2,500, rate 1 per 100, minimum 500 → **25 points**.

## Reversal (returns)

- Each return reverses a share of the points originally earned on that sale.
- Reversal = `floor(earnedPoints × returnedAmount / saleTotal)`.
- Partial return example: earned 25 on LKR 2,500, return LKR 1,250 → reverse `floor(25 × 0.5) = 12`; balance 13.
- A full return reverses all remaining earned points for the sale.
- Reversals are capped so the total reversed for a sale never exceeds the points originally earned, and the balance is never driven negative.
- Reversal is skipped when the sale earned nothing.

## Ledger integrity

- Every change is a `LoyaltyTransaction` row: `Earned`, `Reversed`, `Redeemed` (future) or `Adjustment`.
- Each row stores the signed points delta and the running `BalanceAfter`.
- The balance is **never** overwritten; it is always the sum of the ledger.
- A filtered unique index on `SaleId` (where `Type = Earned`) prevents awarding twice for the same sale.
- Awarding and reversal run inside the sale/return transaction (same `DbContext`), so the ledger commits atomically with the sale.

## Explicitly out of scope

- No redemption, no discounts, no reward tiers.
- No manual points balance edits (an `Adjustment` type is reserved for a future admin screen).
- Negative balances are prevented; an adjustment policy that could allow them is not implemented.