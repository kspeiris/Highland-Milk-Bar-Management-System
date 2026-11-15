# Customer Analytics

Read-only analytics derived from completed sales (`Status != Voided`). Implemented in `CustomerQueryService.GetAnalyticsAsync`; displayed in `CustomerAnalyticsWindow`.

## Metrics

| Metric | Calculation |
| --- | --- |
| Total customers | All customer rows. |
| Active customers | `IsActive = true`. |
| New this month | Registered (`CreatedAt`) on/after the first of the current month. |
| Repeat customers | Customers with 2+ completed sales. |
| Total customer sales | Sum of `TotalAmount` over completed sales with a customer. |
| Average customer spending | Total customer sales ÷ total customers. |

## Rankings

- **Top 10 by spending** — highest lifetime `TotalAmount`.
- **Top 10 by frequency** — highest completed purchase count.
- **No recent purchase** — customers whose last completed purchase is older than 90 days.

## Segmentation

Segments are computed from transaction data (never manually assigned). A customer may belong to several; each is labelled, and a single primary label is reported using the priority below.

| Segment | Rule |
| --- | --- |
| New | First completed purchase within the last 30 days |
| Regular | 5+ completed purchases |
| High-Value | Lifetime spending greater than the configured threshold (default LKR 50,000) |
| Inactive | No completed purchase in the last 90 days |
| One-Time | Exactly one completed purchase |

**Primary label priority:** Inactive → New → One-Time → High-Value → Regular.

Customers with zero purchases carry no segment.

## Thresholds

- High-value threshold and minimum purchase count are read from `AppSettings` keys `customer.high_value_threshold` and `customer.regular_min_purchases` when present, otherwise the defaults above.
- Windows of 30 / 90 days are fixed for this phase.

## Notes

- Analytics load one aggregate row per customer (purchase count, spending, first/last purchase), never the full sale set, so the sale table is not materialised into memory.
- Returned sales still count as purchases; the returned value is reported separately in the customer profile, not netted here.