# Checkout Business Rules

Checkout rules and how they are enforced. The financial core lives in `SalesService.CheckoutAsync`; the WPF layer only calls it. Rules POS-01…POS-18 are enforced below (or explicitly noted as UI-level).

| ID | Rule | Enforcement |
| --- | --- | --- |
| POS-01 | Only active products can be sold. | `CheckoutAsync` rejects unknown or inactive products. |
| POS-02 | Stock-tracked products must have sufficient sellable stock. | `InventoryService.AllocateAsync` throws when sellable batches are insufficient; the sale rolls back. |
| POS-03 | Expired batches cannot be allocated. | `AllocateAsync` excludes batches with `ExpiryDate < today`. |
| POS-04 | FEFO/FIFO allocation must be used. | `AllocationPolicy` selects the order; default FEFO. |
| POS-05 | Negative quantities are not permitted. | Lines with `Quantity <= 0` are rejected. |
| POS-06 | Discounts cannot exceed the applicable sale amount. | Line discount > line gross is rejected; order discount > subtotal is rejected; negative discounts rejected. |
| POS-07 | Final payable amount cannot be negative. | `total < 0` is rejected. |
| POS-08 | Completed sales must have valid payment records. | At least one payment; each payment amount > 0. |
| POS-09 | Split payments must equal the amount due. | Non-cash tenders cannot exceed the total; total tendered must be ≥ total. |
| POS-10 | Cash change must be calculated correctly. | `Change = AmountPaid − Total`; cash may overpay, non-cash may not. |
| POS-11 | Sales must be transactionally processed. | All writes occur inside one `BeginTransactionAsync` / commit. |
| POS-12 | Duplicate checkout submissions must be prevented. | UI guards the button while processing (`IsProcessing`); unique invoice numbers also protect the DB. |
| POS-13 | Historical selling prices must be preserved. | `SaleItem` stores name/price/cost snapshots; later product edits never rewrite them. |
| POS-14 | Every completed sale must have a unique invoice number. | `GenerateSaleReceiptNumberAsync` guarantees uniqueness; `IX_Sales_ReceiptNumber` is unique. |
| POS-15 | Completed sales cannot be hard-deleted. | No delete path; returns only. |
| POS-16 | Returns must reference the original sale. | `SalesReturn.SaleId` is required. |
| POS-17 | Returned stock must be validated before re-entering inventory. | Only `ReturnCondition.Restock` restocks; Damaged/Expired/NotForResale do not. |
| POS-18 | Printing errors must not reverse completed sales. | Receipt generation happens after commit and is independent of the transaction. |

## Discount model

- `SaleLineRequest.DiscountAmount` — per-line discount (absolute amount).
- `SaleCheckoutRequest.OrderDiscount` — order-level discount (absolute amount).
- `Sale.Subtotal` = gross (sum of `qty × unitPrice` before discounts).
- `Sale.DiscountAmount` = line discounts + order discount.
- `Sale.TotalAmount` = subtotal − discount amount.
- Percentage discounts are computed in the UI and passed as an absolute amount.

## Payment rules

- Every payment must be > 0.
- Non-cash tenders (card/bank/other) **cannot exceed** the total (no cash change from card).
- Cash may overpay; the excess is returned as change.
- Total tendered must be ≥ the total due.

## Money & rounding

`decimal` throughout; amounts stored to 2 dp, quantities to 3 dp, unit costs to 4 dp. Currency from `AppSetting("currency.code")` (default LKR). No tax is applied (configurable later; disabled by default).