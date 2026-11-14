# Product Requirements — Highland Milk Bar Management System

**Version:** 1.0 starter brief  
**Business model:** One Highland Milk Bar outlet  
**Application:** Single-user Windows desktop, offline-first  
**Primary currency:** Sri Lankan Rupees (LKR)

## 1. Product vision

Provide one reliable desktop application to manage a Highland Milk Bar that sells packaged dairy products, ice cream, beverages and prepared milk-bar menu items. The application should support daily cashier work and accurate stock/financial records without depending on an internet connection.

## 2. User and operating assumptions

- Initial release is for one Windows PC and one operator.
- No multi-user roles or remote cloud sync are in the first release.
- Store data locally in SQLite under the current Windows user's LocalAppData folder.
- All money values are in LKR and should use decimal arithmetic, not floating point.
- Support fractional stock units where relevant (litres, kilograms, grams, millilitres) as well as discrete units (bottle, cup, pack, scoop).
- UI should be understandable for a shop operator: large POS targets, clear totals, keyboard-friendly input, confirmation for destructive actions and visible feedback for saved/failed operations.

## 3. Functional scope

### 3.1 Dashboard

- Today's gross sales/revenue from completed sales.
- Completed transaction count.
- Active product count.
- Low-stock product count.
- Later: sales trend, popular items, near-expiry batches, recent transactions and quick navigation.
- Dashboard figures must be calculated from stored transactions, not hard-coded demo numbers.

### 3.2 Product and category management

- Create, view, edit, deactivate and search products.
- Product fields: SKU, barcode, name, category, product type, unit, cost price, selling price, reorder level, inventory tracking flag, active flag.
- Product types: packaged, ingredient, prepared/menu item.
- Category CRUD with active/inactive state and display order.
- SKU must be unique. Barcode should be unique when supplied.
- Product names and prices used in historic sale lines must be snapshots so changing a product later does not rewrite old receipts.

### 3.3 Point of Sale

- Fast product search and category filtering; optional barcode scanner support as keyboard-wedge input.
- Cart line add/remove, quantity edit, unit price, line discount, order discount and subtotal/total.
- Payment methods: cash, card, bank transfer/other; split tender is supported.
- Validate tender total equals the sale total before commit.
- Complete sale and stock deduction must be one atomic database transaction.
- Generate a unique receipt number and retain sale/payment/item records.
- Print thermal receipts (58mm and 80mm) and optionally A4 reports.
- Return/void workflows require reason, operator confirmation and stock/payment reversal rules. Never silently delete a completed sale.

### 3.4 Packaged product inventory

- Opening stock, stock receipts, adjustments, waste/damage, expiry and returns.
- Batch/lot number, received date, manufacture date (optional), expiry date (optional), received quantity, remaining quantity and unit cost.
- FIFO for stock allocation, with FEFO (earliest expiry first) preferred for perishable goods when expiry is known. The final policy must be confirmed with the outlet owner.
- Stock ledger records every quantity change with movement type, date, product, batch, quantity delta, source reference and note.
- Prevent negative stock by default; if the owner later permits it, make that an explicit setting and log it.
- Low-stock and near-expiry thresholds must be configurable.

### 3.5 Milk-bar menu, recipe and ingredients

- Create prepared menu products such as milkshakes, ice-cream cups/cones and fresh milk beverages.
- Define each recipe using inventory ingredient products and fractional quantities/units.
- Calculate recipe cost from current ingredient cost data, while preserving cost snapshots for completed sales.
- Completing a menu sale must consume the recipe ingredients and create stock movements in the same database transaction as sale and payment creation.
- Missing recipe, insufficient ingredient stock or inactive ingredient must block checkout with a clear message unless a deliberately designed override is approved.
- Recipe revisions must not change the cost/stock interpretation of historic transactions.

### 3.6 Purchases and suppliers

- Supplier details: business/name, contact person, phone, email, address, notes and active flag.
- Purchase header: supplier, supplier invoice number, purchase date, subtotal, discount, total, amount paid, balance, status and notes.
- Purchase lines: product, quantity, unit cost, batch number, expiry date and line total.
- Receiving a purchase creates inventory batches and stock movements; draft/cancelled purchases must not increase stock.
- Avoid duplicate supplier invoice handling where practical.

### 3.7 Customers

- Optional customer name, phone, email, address and created date.
- Customer capture must not slow down walk-in checkout; a sale can be anonymous.
- Later: customer purchase history and account balances only if credit sales are approved.

### 3.8 Expenses

- Expense categories: utilities, transport, packaging, maintenance, rent, wages and other (editable).
- Expense fields: description, amount, date, payment method, reference, notes and category.
- Expense records must be included in cash-flow and net-profit reporting according to the selected accounting treatment.

### 3.9 Finance and reports

- Daily/weekly/monthly sales summary.
- Product/category sales, payment method summary, purchase summary and stock valuation.
- Profit-and-loss report must distinguish revenue, discounts/returns, cost of goods sold, gross profit, operating expenses and net profit.
- Use historical cost snapshots or a documented costing method; do not calculate historical profit from today's product cost.
- Cash flow should reflect cash movement, not simply sales total.
- Exports: PDF and Excel with date filters and outlet details.

### 3.10 Settings, data safety and backup

- Outlet name/address/contact details, currency display and receipt preferences.
- Configurable tax/discount settings only after the outlet's actual needs are confirmed.
- Manual and scheduled backup to a user-selected folder.
- Restore must validate file integrity, show the backup timestamp and create a safety backup before replacing data.
- Add an exportable backup file; do not rely on the raw SQLite file being copied while open without a safe backup mechanism.
- Data integrity check and error log.

## 4. Non-functional requirements

- Windows 10/11 support, targeting a supported .NET LTS runtime.
- Offline-first; core transactions must not require network access.
- Fast search and normal checkout responsiveness on a typical office PC.
- MVVM presentation separation and clean separation of Domain, Application, Infrastructure and WPF.
- Database access only through Infrastructure/services, never directly from XAML code.
- Use async database calls where practical; avoid blocking the UI thread.
- Use database transactions for sale completion, purchase receiving, recipe consumption, returns and stock corrections.
- Validate user input in both UI and application/service layer.
- Accessible font sizes, high contrast, consistent spacing, keyboard navigation and meaningful empty/error states.
- Maintain audit history for inventory and financial operations.

## 5. Data model baseline

Starter entities included: Category, Product, Supplier, Customer, Sale, SaleItem, Payment, Purchase, PurchaseItem, InventoryBatch, StockMovement, Recipe, RecipeItem, ExpenseCategory, Expense and AppSetting.

Further implementation should review the need for: return headers/lines, cash sessions, stock adjustment headers/lines, tax configuration, backup metadata and application audit logs.

## 6. Acceptance criteria for production readiness

- A completed sale saves sale header, lines and payments atomically and deducts correct packaged/recipe stock exactly once.
- Reopening the app preserves products, transactions and settings.
- A failed checkout leaves no partial sale and does not change stock.
- Purchase receiving adds correct batch stock exactly once.
- Expiry/FIFO allocation is deterministic and traceable.
- Return/void operations reverse the correct financial and stock effects while retaining the original sale.
- Daily/monthly sales totals reconcile to transaction details and payment records.
- Profit reports use historical cost, not current catalogue price.
- Backup can be created and restored with validation and rollback safeguards.
- Tests cover calculation, persistence, edge cases and failure rollback.

## 7. Delivery phases

1. Starter shell, database baseline, dashboard and product catalogue (this ZIP).
2. Database migrations, category/product edit/deactivate, settings and backup foundation.
3. POS cart, checkout, payment splitting, receipt number and transactional stock deduction.
4. Inventory ledger, batches, expiry and FIFO/FEFO rules.
5. Purchases, supplier management and receiving.
6. Milk-bar recipes and ingredient consumption.
7. Expense, finance and reconciled reports.
8. PDF/Excel export, thermal printing, backup/restore UX and installer.
9. Automated tests, user acceptance testing and production hardening.
