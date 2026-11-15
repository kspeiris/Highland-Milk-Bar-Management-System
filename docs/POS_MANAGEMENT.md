# POS & Billing (Point of Sale)

The Point of Sale is the operational heart of the system: browse products, build a cart, take payment, print a receipt and deduct stock — all atomically. Built in **Step 06** on top of the existing `SalesService`, `InventoryService`, `RecipeService` and the Step 03 design system.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `ISalesService` | Commands: `CheckoutAsync` (atomic sale) and `ProcessReturnAsync`. |
| Application | `ISalesQueryService` | Read-only sales history, sale details and customer lookup. |
| Application | `IReceiptService` | Builds a `ReceiptDocument` from a sale + outlet settings. |
| Application | `SalesDtos.cs`, `SalesQueryDtos.cs`, `ReceiptDtos.cs` | Cart/payment/return DTOs, history/detail DTOs, receipt model. |
| Infrastructure | `SalesService`, `ReceiptService`, `ReceiptTextRenderer`, `ReceiptPdfWriter` | Persistence + receipt text/PDF. |
| WPF | `PosViewModel` / `PosView` | Product browsing, cart, payment, checkout. |
| WPF | `SalesHistoryViewModel` / `SalesHistoryView` | History grid, details, reprint, returns. |
| WPF | `SaleDetailsWindow`, `SalesReturnWindow`, `ReceiptWindow` | Details, returns and receipt preview/print. |

There is **one** sales engine (`SalesService`). The UI never writes stock or sales rows directly.

## POS layout

- **Left:** search box (name/SKU/barcode), category filter and a responsive product grid. Each card shows name, selling price and availability.
- **Right:** the current order (cart), line controls (− / qty / + / remove), order discount, total, payment method, cash/card inputs, change and the **Checkout** button.

Only **active** products appear. Out-of-stock tracked products are blocked when added.

## Cart

- Add a product by clicking its card (or scanning its barcode into the search box).
- Increase/decrease/remove the selected line; type a quantity directly.
- Per-line discount and an order-level discount are supported (entered as amounts).
- Stock is **not** deducted when items are added — only at checkout.

## Checkout workflow (atomic)

1. Validate the cart is non-empty, every product exists and is active, quantities are positive and discounts are valid.
2. Revalidate payments (see below).
3. Open a single database transaction and, within it:
   - create the `Sale` + `SaleItem`s (with name/price/cost snapshots) and `Payment` rows;
   - allocate stock via `InventoryService.AllocateAsync` (FEFO/FIFO, expired batches excluded);
   - consume recipe ingredients for prepared items;
   - record `StockMovement` and `StockAllocation` rows.
4. Commit. On any failure the whole transaction rolls back and the cart is preserved.

A committed sale is **never** deleted or reversed because of a printing failure — the receipt is generated separately.

## Receipts

After a successful checkout a `ReceiptDocument` is built (from `AppSetting` outlet details) and shown in a preview window with **Print** and **Save PDF**. See `RECEIPT_AND_PRINTING.md`.

## Sales History

A dedicated screen lists sales with invoice, date, item count, payment summary, discount, total and status. Filter by invoice/date/payment method/status, page through results, and open **Details**, **Reprint receipt** or **Process return**.

## Keyboard / scanner notes

- Barcode scanners that act as keyboards work by typing the barcode into the search box and pressing Enter.
- The checkout button is guarded so a double-click cannot submit twice.

## Milk Bar integration

- **Packaged products** deduct their own inventory.
- **Prepared (recipe-based) products** deduct their recipe ingredients (FEFO/FIFO) in the same transaction. If a prepared item has **no active recipe**, checkout is rejected rather than deducting nothing.

Recipe management UI is delivered in Step 07; the POS already consumes recipes.