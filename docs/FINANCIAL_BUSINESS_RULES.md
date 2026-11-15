# Financial Business Rules

Formulas and guarantees for the Step 11 financial layer. All amounts are LKR and rounded to 2 decimals (`AwayFromZero`).

## Profit & loss

- **Net sales** = Gross sales − Discounts − Sales returns.
- **Gross profit** = Net sales − Cost of goods sold.
- **Operating profit** = Gross profit − Operating expenses.
- Gross sales/discounts/COGS use **non-voided** sales; sales returns use **completed** returns. Only rows inside the selected window (by sale/return/expense date) are counted.

## Margins

- **Gross margin %** = Gross profit ÷ Net sales × 100 (0 when net sales is 0).
- **Net margin %** = Operating profit ÷ Net sales × 100 (0 when net sales is 0).

## Cash flow (cash basis)

- **Cash in** = cash `Payment` on non-voided sales (plus PayIn movements when present).
- **Cash out** = cash `SupplierPayment` + cash `Expense` (plus Payout movements when present).
- **Net operating cash flow** = cash in − cash out. Non-cash methods are excluded here and appear only in Payment Analysis.

## Payables

- Outstanding uses **received** purchases only and counts **all unpaid** amounts, not only overdue.
- **Overdue** = unpaid portion of purchases whose `DueDate` is before today.
- **Ageing** buckets (by earliest unpaid due date): Current, 1–30, 31–60, 61–90, 90+ days.
- Suppliers with a zero balance are excluded from the liability lines.

## Cash reconciliation

- **Expected closing** = opening float + session cash in − session cash out.
- **Variance** = counted closing − expected closing (only when the session is closed).

## Read-only guarantee

`FinancialQueryService` never calls `SaveChanges`. Financial reporting cannot alter sales, purchases, inventory, cash balances or the customer/loyalty ledgers. A test (`FinancialReporting_IsReadOnly`) asserts row counts are unchanged after reporting.