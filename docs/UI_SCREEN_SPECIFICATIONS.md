# UI Screen Specifications

Per-screen layout and data specifications for the Step 03 shell. Screens marked **Scaffold** are reserved placeholders; screens marked **Implemented** are built on the design system.

## Dashboard — Implemented

Rebuilt on the design system. Data sources:

- **Real** (from `IDashboardService.GetSnapshotAsync`): revenue today, transactions today, active products, low-stock count.
- **Demo** (sample data, clearly labelled `DEMO`): profit today, expenses today, sales trend, category split, top products, inventory alerts, recent transactions.

### Layout (top to bottom)

1. **Page header** — "Dashboard", subtitle from `Greeting` ("Good morning/afternoon/evening — here is what is happening at your outlet today."), right pill with `TodayText`.
2. **KPI row** — `UniformGrid` 4 columns of `StatCard`:
   - TODAY'S REVENUE (real) — `RevenueTodayText`, primary accent.
   - TODAY'S PROFIT (demo) — `ProfitTodayText`, success accent, `IsDemo`.
   - TRANSACTIONS (real) — `TransactionsToday`, accent.
   - TODAY'S EXPENSES (demo) — `ExpensesTodayText`, warning accent, `IsDemo`.
3. **Analytics row** (1.5 : 1):
   - Sales trend card (last 7 days) — `InsetPanel` with 7 bar placeholders, `DEMO` badge.
   - Sales by category — `ItemsControl` of `CategorySaleRow` (category, percent bar, amount).
4. **Two-up row** (1 : 1):
   - Top-selling products — `ItemsControl` of `TopProductRow` (name, units, amount).
   - Inventory alerts — grouped `LOW STOCK`, `NEAR EXPIRY`, `EXPIRED` lists (`StockAlertRow`).
5. **Recent transactions** — `AppDataGrid` bound to `RecentSales` (`RecentSaleRow`): invoice, date, items, payment, total, status. `DEMO DATA` badge.
6. **Demo note** — `DemoNote` caption explaining demo figures.

Each card uses `AppCard`/`FlatCard` and `CardHeadingText` + `SecondaryText`. Status uses badges (never colour alone).

## Products — Implemented (existing)

Product catalogue list/search/add (from Step 02). Reuse `AppDataGrid`, `InputTextBox`, and the button styles for consistency. Edit, deactivate/reactivate, category management and opening-stock-through-movement are the next catalogue tasks (not part of Step 03 UI shell).

## Scaffold modules

Each renders `PlaceholderView`: `PageHeader` (title + subtitle), disabled `SearchBox` toolbar with `Status` + planned `Step` badges, and an `EmptyState` describing the planned workspace.

| Module | Planned step | Scope (summary) |
| --- | --- | --- |
| Point of Sale | Step 06 | Product search, cart, discounts, split payment, receipts, returns; built on `ISalesService.CheckoutAsync` (atomic). |
| Inventory | Step 05 | Stock-in/out, adjustments, batches, expiry, FIFO/FEFO allocation, movement history (services exist). |
| Milk Bar | Step 07 | Prepared menu items, recipes, ingredient consumption via recipe services. |
| Purchases | Step 08 | Supplier invoices, quantities, batch/expiry, costs, goods received. |
| Suppliers | Step 08 | Contact directory, purchase history, outstanding balances. |
| Customers | Step 09 | Customer profiles and purchase history. |
| Expenses | Step 10 | Operating expenses (utilities, transport, packaging, maintenance, etc.). |
| Finance | Step 11 | Revenue, COGS, gross profit, operating expenses, net profit, cash flow (never conflate revenue and profit). |
| Reports | Step 13 | Daily/monthly sales, product, inventory, expiry, purchase, expense, P&L; PDF/Excel export. |
| Settings | Step 14 | Outlet details, receipt settings, DB location, backup/restore, preferences. |

## Cross-screen requirements

- Every built screen needs loading, empty, validation, success and failure states.
- POS is keyboard-first; scanner input must work as keyboard input.
- Confirm destructive actions; void/return rather than delete completed sales.
- No invented Highland price data or fake business results in real (non-demo) areas.
- App must remain usable at 1120×720 (minimum window size).