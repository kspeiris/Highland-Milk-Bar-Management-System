# Coding Agent Handoff — Start Here

You are continuing the `HighlandMilkBar` WPF solution. Read `README.md`, `PRODUCT_REQUIREMENTS.md` and `DESIGN_SYSTEM.md` before making changes.

## Non-negotiable decisions

- Windows desktop only; single-user, offline-first.
- C# / .NET 10 LTS, WPF + XAML, SQLite, EF Core 10.
- Keep the 4-project separation: Domain, Application, Infrastructure, WPF.
- Preserve MVVM: views should not issue SQL or access `AppDbContext` directly.
- Data lives under `%LOCALAPPDATA%\HighlandMilkBar\highland-milkbar.db`.
- Currency: LKR. Use `decimal` for all monetary and quantity calculations.
- Keep existing Highland blue design system and hybrid POS/ERP layout.

## Current baseline

- Solution and project references are present.
- `AppDbContext` contains the starting data model.
- `DbInitializer` applies EF Core migrations and seeds reference data (units, categories, starter products, expense categories, default settings). It never drops or recreates user data.
- Database model is complete for Step 02 (27 tables). Entity mapping lives in `Infrastructure/Data/Configurations`. Migrations live in `Infrastructure/Data/Migrations`.
- FIFO/FEFO allocation, purchase receiving, recipe costing/consumption, atomic checkout, returns and SQLite backup/restore are implemented in `Infrastructure/Services` behind interfaces in `Application/Services`.
- Automated integration tests exist in `tests/HighlandMilkBar.Tests` (real temporary SQLite, migrations applied).
- Dashboard is connected to SQLite for revenue, completed transaction count, active products and low-stock counts.
- Product catalogue supports list/search/add.
- Navigation placeholders exist for POS, Inventory, Milk Bar, Purchases, Suppliers, Customers, Expenses, Finance, Reports and Settings.

## First tasks for the next coding session

1. Build and run the solution on Windows with the .NET 10 SDK (done — builds clean).
2. Automated tests for the database foundation (done — `tests/HighlandMilkBar.Tests`, 14 passing).
3. EF Core migrations + safe upgrade path (done — `InitialCreate`; `DbInitializer` uses `MigrateAsync`).
4. Finish catalogue CRUD: edit, deactivate/reactivate, category management, barcode uniqueness, validation and stock opening balance through a stock movement rather than directly editing current stock.
5. POS checkout UI on top of `ISalesService.CheckoutAsync` (the atomic transaction service already exists); then inventory, purchase, recipe and finance modules.

## POS transaction invariant

One database transaction must persist the sale header, sale lines, payment lines, stock movements and stock/batch balance changes. If any validation or save fails, rollback everything. Do not let the UI save sale/payment/stock pieces independently.

## Inventory invariant

All stock changes must be traceable through `StockMovement`. Do not directly update `Product.CurrentStock` from random UI handlers. Use one inventory service to apply a movement and keep the product aggregate/batch totals consistent. Define and test FEFO/FIFO policy with the outlet owner before enabling expiry allocation.

## Recipe invariant

A prepared menu item is sold as a sale line but consumes ingredient products according to the recipe version used at sale time. Preserve unit conversion, ingredient quantity, and cost snapshots. Sale and ingredient deductions must commit atomically.

## Finance invariant

Never calculate historic COGS using today's product cost. Sale line cost snapshots (or a documented batch-cost allocation method) must support gross profit reporting. Profit and cash flow are different reports.

## UX quality bar

- Every screen needs loading, empty, validation, success and failure states.
- Keyboard-first operation for POS; scanner input should work as keyboard input.
- Confirm destructive actions; use void/return workflows for completed sales.
- Do not introduce fake business results or invented Highland price data.
- Preserve the initial visual tokens and ensure the app remains usable at 1120x720.

## Delivery style

Implement one vertical slice at a time: domain rules, application service, infrastructure persistence, view model, view, tests. Build and run after each meaningful change. Update README and this handoff file whenever architecture or workflows change.
