# Receipt & Printing

Receipts are generated from a completed sale and the outlet's settings. The system supports **58 mm**, **80 mm** thermal roll paper and **A4** plain paper.

## Components

| Type | Responsibility |
| --- | --- |
| `ReceiptDocument` / `ReceiptLine` | Format-agnostic receipt model (outlet, sale, lines, totals, payments, footer). |
| `ReceiptFormat` | `Thermal58`, `Thermal80`, `A4`. |
| `ReceiptService` | Builds a `ReceiptDocument` from a `Sale` + outlet settings. |
| `ReceiptTextRenderer` | Renders fixed-width plain text (monospace-friendly, `Environment.NewLine`). |
| `ReceiptPdfWriter` | Renders a dependency-free single-page PDF (Courier/ASCII). |
| `ReceiptWindow` | Preview (FlowDocument) + **Print** (via `PrintDialog.PrintDocument`) + **Save PDF**. |

## Widths

| Format | Width | Characters/line |
| --- | --- | --- |
| 58 mm | 32 | 32 |
| 80 mm | 48 | 48 |
| A4 | 80 | 80 |

The configured width comes from `AppSetting("receipt.width_mm")` (default **80**).

## Receipt content

- Outlet name, address, phone (from `outlet.name` / `outlet.address` / `outlet.phone`).
- Invoice number, date/time, cashier (if present).
- Line items: description, quantity, unit price, amount.
- Line and order discounts (when non-zero).
- Subtotal, discount total, **total**.
- Payment breakdown per method, cash received and **change**.
- Footer text (`receipt.footer`).

No Highland branding is included.

## Printing

- Preview opens after checkout (and via **Reprint** in Sales History).
- **Print** uses the WPF `PrintDialog` with the plain-text renderer; **Save PDF** uses the dependency-free PDF writer.
- Thermal printing is plain text at the configured width — no external printer driver/model is hard-coded; it prints to whatever printer is selected.

## Failure isolation (POS-18)

Receipt generation and printing happen **after** the sale transaction commits. A printing error is surfaced to the user but **never** rolls back, deletes, or reverses the sale. Staff can reprint from Sales History.

## Saving

A completed sale's receipt can be regenerated at any time from the stored `Sale`, so a lost printout is never a data-loss event.