# Highland Milk Bar Management System – Software Requirements Specification

**Version:** 1.0  
**Date:** 14 November 2026  
**Status:** Baseline specification for development  
**Platform:** Windows desktop, single-user, offline-first  
**Stack:** C# / .NET 10 / WPF / MVVM / SQLite / Entity Framework Core


HIGHLAND

MILK BAR

MANAGEMENT SYSTEM

SOFTWARE REQUIREMENTS SPECIFICATION

Version 1.0  |  14 November 2026

Prepared as the baseline specification for implementation

| DOCUMENT PROFILE | VALUE |
| --- | --- |
| Product | Highland Milk Bar Management System |
| Deployment | Single Windows desktop computer; offline-first |
| Primary operator | One outlet owner / cashier / operator |
| Currency and locale | Sri Lankan Rupee (LKR); Asia/Colombo time zone |
| Technology baseline | C# / .NET 10 / WPF / MVVM / SQLite / Entity Framework Core |
| Document status | Baseline SRS for development; open business policies are marked for confirmation |


# Document Control

| Item | Details |
| --- | --- |
| Document title | Highland Milk Bar Management System – Software Requirements Specification |
| Version | 1.0 |
| Date | 14 November 2026 |
| Status | Baseline specification for development |
| Prepared for | The owner/operator of a single Highland Milk Bar outlet |
| Intended implementation team | Project owner, C#/.NET developer and coding agent |
| Source baseline | The agreed project scope and Starter v0.1 product/design requirements |


## Revision History

| Version | Date | Change summary |
| --- | --- | --- |
| 0.1 | 14 Nov 2026 | Initial requirements baseline created from the agreed outlet scope. |
| 1.0 | 14 Nov 2026 | Consolidated functional, data, UX, operational, quality and acceptance requirements into a complete SRS. |


## Approval and Change Control

This document is the development baseline. The outlet owner should approve operational policies that affect money, stock valuation, expiry handling, refunds, tax display and receipt content before those policies are enabled in a production release. Any requirement change after approval should be recorded with its reason, impact, priority and acceptance criteria.

# Table of Contents

| 1. Introduction | 11. Use Cases |
| --- | --- |
| 2. Product Overview | 12. Reports and Calculation Definitions |
| 3. Scope and Release Boundaries | 13. Acceptance Criteria |
| 4. Users and Operating Environment | 14. Verification and Test Requirements |
| 5. Functional Requirements | 15. Deployment, Backup and Support |
| 6. Business Rules | 16. Risks, Constraints and Open Decisions |
| 7. Data Requirements and Conceptual Model | 17. Delivery Roadmap |
| 8. External Interface Requirements | Appendix A. Glossary |
| 9. UI / UX Requirements | Appendix B. Initial Configuration Defaults |
| 10. Non-Functional Requirements | Appendix C. Coding Agent Implementation Guidance |

> Note: This document describes the target product. The Starter v0.1 ZIP is only an initial runnable shell and product catalogue slice; navigation placeholders are not evidence that the remaining modules have been implemented.

# 1. Introduction

## 1.1 Purpose

This Software Requirements Specification (SRS) defines the complete functional and non-functional requirements for the Highland Milk Bar Management System (HMBMS). It is intended to guide UI/UX design, database design, implementation, testing, deployment and future enhancements. Requirements use unique identifiers so the owner and implementation team can trace each feature to tests and acceptance decisions.

## 1.2 Product Objective

The system will provide one reliable Windows desktop application for operating a single Highland Milk Bar outlet that sells packaged dairy products, ice cream, beverages and prepared milk-bar menu items. It will combine point-of-sale billing, product catalogue, stock and batch control, recipe-based ingredient consumption, purchasing, expenses, financial summaries and business reports. Core operations must work without an internet connection.

## 1.3 Intended Audience
- Outlet owner or person responsible for approving business rules.
- Cashier/operator who performs sales, purchases, stock checks and daily closing.
- C#/.NET developer or coding agent implementing the application.
- Tester responsible for verifying calculations, data integrity and usability.

## 1.4 Document Conventions

| Term | Meaning |
| --- | --- |
| FR | Functional requirement. |
| NFR | Non-functional requirement. |
| BR | Business rule. |
| UC | Use case. |
| P0 / Must | Required for the first production release. |
| P1 / Should | Important; implement in the planned release unless explicitly deferred. |
| P2 / Could | Optional enhancement; not a release blocker. |
| TBD | A business decision that must be confirmed before the affected feature is finalized. |


Requirement priority labels describe delivery importance, not permission to ignore data integrity or security. Transaction consistency, backup safety and correct monetary calculations are mandatory foundations.

## 1.5 References and Related Project Files
- HighlandMilkBar Starter v0.1 – README.md
- docs/PRODUCT_REQUIREMENTS.md
- docs/DESIGN_SYSTEM.md
- docs/CODING_AGENT_HANDOFF.md
- This SRS – version 1.0
- Official Microsoft compatibility and lifecycle references are listed in Appendix D.

# 2. Product Overview

## 2.1 Product Perspective

HMBMS is a standalone desktop business application installed on one Windows PC at one outlet. It uses a local SQLite database and does not require a hosted server, web browser or cloud account. The application follows a WPF presentation layer with MVVM and a layered solution structure separating Domain, Application, Infrastructure and WPF concerns.

## 2.2 High-Level Capabilities

| Capability area | What the system provides |
| --- | --- |
| Daily operations | Dashboard, point of sale, payment capture, receipts and daily closing. |
| Catalogue | Products, categories, saleable packaged items, ingredients and prepared menu items. |
| Stock control | Opening stock, batches, expiry dates, movements, adjustments, damage and alerts. |
| Milk-bar operations | Recipe definition, ingredient unit conversion, recipe cost and automatic ingredient usage. |
| Procurement | Supplier records, purchase entry, receiving, supplier payments and purchase history. |
| Finance | Expenses, sales summaries, cost of goods sold, gross profit, net operating result and cash-flow view. |
| Management information | Search, filtered reports, PDF/Excel export and operational alerts. |
| Data protection | Local data storage, manual/automatic backups, restore validation and audit history. |


## 2.3 Design Principles
- Offline-first: core work is available locally without internet access.
- Single-operator simplicity: no role administration or multi-user synchronization in release 1.0.
- Transaction safety: sale, payment and stock effects either commit together or do not commit.
- Traceability: stock and financial records are reversed or adjusted with an audit trail; completed transactions are not silently deleted.
- Operator-focused UX: fast POS, clear labels, readable totals, visible validation and concise workflows.
- Truthful reporting: use completed transaction records and historical cost snapshots, not hard-coded values or current catalogue cost for historic profit.

## 2.4 Key Operating Assumptions
- The first release serves one outlet on one Windows PC.
- One local operator uses the application; multiple concurrent users and remote access are out of scope.
- The primary currency is LKR. All money calculations use decimal arithmetic.
- The Windows user profile is the owner of the local database and backup location.
- The business owner supplies the real product catalogue, selling prices, purchase costs, opening stock, recipes and outlet details. No official Highland price list or inventory quantity is assumed by the software.
- The outlet uses an external card terminal or bank application if card/bank payments are accepted; release 1.0 records the tender type and amount but does not directly process a bank transaction.

# 3. Scope and Release Boundaries

## 3.1 In Scope
- Windows desktop application with modern hybrid POS/ERP user experience.
- Single-user local operation with SQLite persistence.
- Dashboard and operational alerts.
- Product/category CRUD, optional barcode field and product search.
- Packaged product inventory with stock batches, stock movements, expiry and low-stock tracking.
- Milk-bar menu items, recipes and ingredient inventory with recipe-based deductions.
- POS cart, discounts, payment methods, split tenders, receipt generation and sales history.
- Purchase records, receiving, suppliers, supplier balances and supplier payment records.
- Optional customer records and customer purchase history; anonymous walk-in sales remain supported.
- Expense entry and financial reports.
- Daily/weekly/monthly reporting, PDF and Excel export.
- Receipt configuration, application preferences, backup/restore and audit trail.
- Basic automated tests, installer and production readiness checks.

## 3.2 Out of Scope for Release 1.0
- Cloud synchronization, multi-branch management, mobile application or web portal.
- Multiple concurrent user accounts, permission roles or remote cashier access.
- Direct integration to bank/card payment networks, QR payment providers or accounting packages.
- Online ordering, delivery dispatch, e-commerce, loyalty points, marketing automation or customer messaging.
- Payroll, full general ledger, statutory financial statements, tax filing or inventory procurement automation.
- Automatic connection to MILCO/Highland corporate systems or supplier APIs unless a documented interface is later provided.
- Networked multi-terminal POS operation.

## 3.3 Future Extension Possibilities

The architecture should avoid blocking later additions such as multi-outlet synchronization, role-based access, customer credit accounts, loyalty, supplier imports, a cloud backup destination and external payment integrations. These are not release 1.0 commitments and must not introduce unnecessary complexity into the initial delivery.

# 4. Users and Operating Environment

## 4.1 Primary User Profile

| User | Responsibilities | System access |
| --- | --- | --- |
| Outlet owner/operator (single user) | Configure outlet; maintain products and recipes; record purchases and expenses; sell products; review stock, cash and reports; perform backups. | All application functions. No role switching is required in release 1.0. |


## 4.2 Operating Environment

| Area | Requirement |
| --- | --- |
| Operating system | Windows 11 64-bit is the primary supported target. Windows 10 may be considered only for editions/versions still listed as supported by Microsoft for .NET 10 (such as qualifying LTSC/Enterprise editions) and covered by applicable security servicing; ordinary Windows 10 Home/Pro is not the production baseline. |
| Runtime | Target .NET 10 LTS for Windows desktop and keep the application on the latest supported servicing patch. As checked on 14 Nov 2026, .NET 10 is active through 14 Nov 2028. Package self-contained or provide a clear runtime prerequisite. |
| UI framework | Windows Presentation Foundation (WPF) with XAML and MVVM. |
| Persistence | SQLite database via Entity Framework Core; schema changes through EF Core migrations after the starter phase. |
| Hardware baseline | Typical office PC; target at least 8 GB RAM and modern dual-core CPU. POS should remain responsive on modest hardware. |
| Display | Optimized for 1366×768 or higher; remain usable at 1120×720. Support Windows display scaling. |
| Peripherals | Optional USB barcode scanner in keyboard-wedge mode; optional 58 mm / 80 mm thermal printer; optional A4 printer. |
| Network | No network required for core usage. Internet may be needed once for initial package restore/installation only. |


## 4.3 Localization and Regional Defaults
- Currency: LKR, displayed using Sri Lankan number formatting where available.
- Time zone: Asia/Colombo; persist timestamps consistently and display local time.
- Default date display: dd MMM yyyy; use an unambiguous date picker for entry and filtering.
- Default decimal precision: two decimal places for LKR; quantity precision configurable by unit (e.g., whole pieces, 3 decimals for litres/kg, higher precision for ml/g where needed).
- Application language: English for release 1.0. Sinhala/Tamil localization may be considered later.

# 5. Functional Requirements

Each requirement below is independently testable. Unless a requirement is marked P1 or P2, treat it as P0 / Must for the first production release.

## 5.1 Application Shell and Navigation

The application shall present a consistent branded desktop shell with persistent navigation and clear page state.

FR-001 [P0] The system shall open to the Dashboard after successful initialization, unless a configured startup page is later introduced.

FR-002 [P0] The shell shall provide navigation to Dashboard, Point of Sale, Products, Inventory, Milk Bar, Purchases, Suppliers, Customers, Expenses, Finance, Reports and Settings.

FR-003 [P0] The system shall display the current outlet name, current local date/time and current page title in the application shell.

FR-004 [P0] The system shall display clear loading, empty, success, validation and failure states. It shall prevent duplicate submission while a transaction is being processed.

FR-005 [P1] The operator shall be able to use keyboard shortcuts for frequent actions, including opening POS, focusing product search and starting a new sale.

FR-006 [P0] A recoverable application error shall be logged locally with timestamp and technical detail; the UI shall show a safe, readable message rather than an unhandled stack trace.

## 5.2 Dashboard

Dashboard values must be calculated from the local database and use a consistent date range.

FR-010 [P0] The dashboard shall display today’s completed sales total, completed transaction count, active product count and low-stock item count.

FR-011 [P1] The dashboard shall display daily/weekly/monthly sales trend, best-selling products, recent transactions and payment-method summary.

FR-012 [P1] The dashboard shall display near-expiry batches, expired batches and items below reorder level.

FR-013 [P0] The dashboard shall allow the operator to navigate from a KPI or alert to the corresponding filtered detail screen.

FR-014 [P0] Dashboard totals shall exclude draft, failed and voided sales; returns shall be reflected in net sales using the report definitions in Section 12.

## 5.3 Product and Category Management

The catalogue shall support packaged goods, ingredients and prepared/menu items.

FR-020 [P0] The operator shall be able to create, view, edit, search, filter, activate and deactivate product categories.

FR-021 [P0] The operator shall be able to create, view, edit, search, filter, activate and deactivate products.

FR-022 [P0] A product shall support SKU, optional barcode, name, category, description, product type, base stock unit, purchase/cost price, selling price, reorder level, inventory-tracked flag, sellable flag and active flag.

FR-023 [P0] The system shall prevent duplicate SKUs and duplicate non-empty barcodes, ignoring case and surrounding spaces.

FR-024 [P0] The system shall validate required fields, positive or non-negative monetary values and valid unit/type combinations before saving.

FR-025 [P0] Products referenced by transactions shall be deactivated rather than hard-deleted. Historic sale and purchase lines shall preserve product-name, SKU, unit, price and cost snapshots.

FR-026 [P0] The operator shall be able to assign a product to an appropriate category and choose a type: Packaged, Ingredient or Prepared/Menu Item.

FR-027 [P1] A product may be both sellable and usable as an ingredient when the business needs that behaviour.

FR-028 [P1] The catalogue shall support product images or icons for POS tiles; image storage shall be optional and shall not block sales if no image exists.

## 5.4 Point of Sale (POS)

POS is the primary fast workflow. The operator must be able to complete a walk-in sale without creating a customer record.

FR-030 [P0] The POS screen shall provide product search, category filtering and a selectable product grid/list.

FR-031 [P0] The operator shall be able to add, remove and change quantities of cart lines and see line totals, subtotal, discounts and amount due.

FR-032 [P0] The system shall validate that a product is active and sellable, and that requested quantities and prices are valid.

FR-033 [P0] The system shall support line-level and order-level discounts, subject to configured maximums and an audit reason where configured.

FR-034 [P0] The system shall support cash, card, bank transfer and Other payment methods. The payment screen shall support split tenders.

FR-035 [P0] The system shall require the sum of captured payment amounts to equal the amount due before a sale can be completed. Cash change shall be calculated and shown separately.

FR-036 [P0] The operator shall be able to attach an optional customer to a sale; anonymous walk-in sales shall be supported by default.

FR-037 [P0] The system shall assign a unique, readable receipt number to each completed sale.

FR-038 [P0] The system shall commit sale header, sale lines, payment lines, stock allocations, stock movements and updated stock balances in one atomic database transaction.

FR-039 [P0] If any checkout validation or persistence step fails, the system shall roll back every part of the checkout and leave stock and financial records unchanged.

FR-040 [P0] The POS shall display a clear success state after checkout and offer receipt print/reprint and a view of the completed transaction.

FR-041 [P1] The system shall accept barcode scanner input that behaves as keyboard input; scanning a known barcode shall add the corresponding sellable product to the cart.

FR-042 [P1] The system shall allow a sale to be held/suspended and resumed without deducting stock until checkout completes.

FR-043 [P0] By default, stock tracked items shall not be sold below available quantity. Any future override must be an explicit setting and create an audit record.

FR-044 [P1] The POS shall provide keyboard-first operation, predictable focus order and large touch-friendly controls for common actions.

## 5.5 Payments and Receipt Management

Release 1.0 records payment tenders; it does not directly move money through a bank/card network.

FR-050 [P0] A sale may contain one or more payment records; each payment shall store method, amount, timestamp and optional reference.

FR-051 [P0] The application shall not mark a sale paid unless the payment total reconciles with the sale amount due.

FR-052 [P0] Receipt details shall include outlet details, receipt number, date/time, item lines, quantities, unit prices, discounts, total, tender breakdown and change where relevant.

FR-053 [P0] Receipt layout shall support 80 mm and 58 mm thermal paper through configurable templates; A4 output shall be available for reports and optional invoice print.

FR-054 [P1] The operator shall be able to reprint a receipt from sales history; reprinting shall not create a second sale or payment.

FR-055 [P0] Receipt tax fields shall be configurable only after the outlet owner confirms the actual tax requirements. The system shall not assume a tax rate.

## 5.6 Sales History, Returns and Voids

Completed sales must be retained for traceability.

FR-060 [P0] The operator shall be able to search sales by receipt number, date range, customer and payment method.

FR-061 [P0] The operator shall be able to view the complete header, items, payments and stock-cost details for a sale.

FR-062 [P0] The operator shall be able to create a full or partial return against a completed sale, limited to quantities not already returned.

FR-063 [P0] A return shall record returned lines, quantity, refund amount, reason, restock decision, refund tender and timestamp.

FR-064 [P0] A return shall reverse the correct financial values and, only where the returned goods are marked resaleable, restore stock through a traceable stock movement.

FR-065 [P0] Prepared beverages and consumed food items shall not automatically return ingredients to stock; the operator shall record waste/disposal or follow an approved outlet policy.

FR-066 [P0] Voiding a completed sale shall require confirmation and a reason and shall create linked reversal records; the original transaction shall remain visible.

FR-067 [P0] The system shall prevent return quantities or refund amounts from exceeding the remaining eligible sold quantity/value.

## 5.7 Inventory and Batch Management

All stock balances must be derived from controlled stock movements and batch balances.

FR-070 [P0] The system shall show on-hand, reserved/held if later supported, available and reorder-level quantities for each stock-tracked product.

FR-071 [P0] The operator shall record opening stock, purchase receipt, sale consumption, sale return, waste/damage, expiry write-off and stock adjustment.

FR-072 [P0] Each stock movement shall record product, date/time, movement type, quantity delta, unit, source reference, reason/note and resulting balance or enough information to reconstruct it.

FR-073 [P0] A received inventory batch shall record product, batch/lot number when available, received date, optional manufacture date, optional expiry date, received quantity, remaining quantity and unit cost.

FR-074 [P0] The system shall support fractional quantities using decimal values for litres, millilitres, kilograms and grams, as well as discrete units such as pieces, bottles, packs, cups and scoops.

FR-075 [P0] The system shall support configurable reorder levels and near-expiry alert windows.

FR-076 [P0] Expired stock shall be visibly flagged and excluded from normal POS availability unless an explicitly approved and audited override is later introduced.

FR-077 [P0] For perishable stock with expiry dates, allocation shall prefer FEFO (earliest expiry first); where expiry is absent, use FIFO by received date. The owner shall approve this policy before production use.

FR-078 [P0] The system shall prevent negative stock by default and shall block checkout with a clear explanation if eligible stock is insufficient.

FR-079 [P0] Stock corrections, waste and damage shall require a reason and shall create a permanent stock movement record.

FR-080 [P1] The inventory screen shall provide batch detail, expiry filters, stock movement history and stock valuation view.

FR-081 [P1] The system shall support batch-level cost allocation so that the cost of goods sold can be calculated from the actual stock batches consumed.

## 5.8 Milk-Bar Menu and Recipe Management

Prepared menu products consume ingredients according to a saved recipe version.

FR-090 [P0] The operator shall create, view, edit, activate and deactivate prepared/menu items such as milkshakes, ice-cream cups/cones and beverages.

FR-091 [P0] The operator shall define a recipe with ingredient product, quantity, unit, preparation notes and optional wastage allowance.

FR-092 [P0] The system shall validate that each recipe ingredient is active, stock-tracked and dimensionally compatible with the recipe unit.

FR-093 [P0] The system shall support controlled unit conversion within the same measurement dimension (e.g., kg/g and L/ml); it shall reject incompatible conversions (e.g., grams to millilitres) unless a product-specific conversion is explicitly defined.

FR-094 [P0] The system shall calculate estimated recipe cost from ingredient batch costs/current cost layers and display the basis and timestamp of the estimate.

FR-095 [P0] At checkout, a prepared/menu sale line shall consume its required ingredient quantities and create ingredient stock movements in the same transaction as the sale and payments.

FR-096 [P0] The system shall block prepared-item checkout if no active recipe exists or required ingredient stock is insufficient.

FR-097 [P0] A recipe edit shall create a new effective version or immutable version snapshot; it shall not change the ingredient or cost interpretation of previous sales.

FR-098 [P1] The system shall support optional modifiers/variants (e.g., size or add-on) with separately defined recipes and prices.

FR-099 [P1] The operator shall be able to view recipe cost, selling price, estimated gross margin and ingredient usage history.

## 5.9 Purchase and Supplier Management

Purchases increase inventory only when received.

FR-100 [P0] The operator shall create, edit while draft, view and cancel purchase records.

FR-101 [P0] A purchase shall contain supplier, supplier invoice/reference, purchase date, line items, quantities, unit costs, line totals, discounts, subtotal, total, amount paid and balance.

FR-102 [P0] Purchase lines shall allow batch number, expiry date and received quantity for stock-tracked goods.

FR-103 [P0] Only a purchase marked Received shall add stock batches and corresponding stock movements.

FR-104 [P0] Receiving shall be atomic and idempotent: a repeated receive action must not create duplicate stock.

FR-105 [P0] The system shall record supplier payments and calculate outstanding supplier balance from purchases, payments and approved purchase returns/adjustments.

FR-106 [P0] Supplier records shall support business name, contact person, phone, email, address, notes and active/inactive state.

FR-107 [P1] The system should warn about a likely duplicate supplier invoice for the same supplier, but allow an authorized single operator to proceed with a recorded reason.

FR-108 [P1] The system shall provide purchase history and supplier-wise purchase/payment summaries.

## 5.10 Customer Management

Customer capture is optional and must not slow walk-in sales.

FR-110 [P1] The operator shall create, edit, search, view and deactivate customer records.

FR-111 [P1] Customer records shall support name, phone, email, address, notes and created date.

FR-112 [P0] A sale shall be allowed without a customer record.

FR-113 [P1] The system shall show a customer’s linked purchase history where customer data was captured.

FR-114 [P2] Customer credit sales, receivables and customer account statements are not part of release 1.0 unless approved as a scope change.

## 5.11 Expense Management

Expenses support cash-flow and operating result reporting.

FR-120 [P0] The operator shall create, view, edit before finalization and search expense records.

FR-121 [P0] An expense shall store date, category, description, amount, payment method, optional reference, notes and status.

FR-122 [P0] Default editable expense categories shall include utilities, transport, packaging, maintenance, rent, wages and other.

FR-123 [P0] An expense edit or void after finalization shall preserve an audit trail and require a reason.

FR-124 [P1] The system shall support recurring-expense reminders or templates; automatic posting shall require operator confirmation.

## 5.12 Finance and Cash Management

The finance module is a management reporting tool, not a complete statutory accounting package.

FR-130 [P0] The system shall calculate net sales, cost of goods sold, gross profit, operating expenses and net operating result according to Section 12.

FR-131 [P0] The system shall distinguish sales revenue from cash received and distinguish purchases from supplier payments.

FR-132 [P0] The system shall show cash/card/bank/other tender summaries for a selected date range.

FR-133 [P1] The system should support a daily cash session with opening float, cash sales, cash refunds, cash expenses, cash paid out, expected cash, counted cash and variance.

FR-134 [P1] The system should show supplier payable balances and payment history.

FR-135 [P0] Historic gross profit shall use recorded sale-time cost or batch-cost allocations, never today’s product cost alone.

FR-136 [P0] All financial calculations shall use decimal data types and an explicitly defined rounding rule.

FR-137 [P1] The operator shall be able to record owner cash-in/drawings or other non-sales cash movements if the daily cash session is implemented; such movements must not be classified as revenue or expenses by default.

## 5.13 Reports and Export

All reports shall show the chosen date range, generation time, outlet identity and filter criteria.

FR-140 [P0] The system shall provide daily, weekly and monthly sales reports.

FR-141 [P0] The system shall provide product-wise and category-wise sales reports.

FR-142 [P0] The system shall provide inventory on-hand, batch, expiry, low-stock, stock movement and stock valuation reports.

FR-143 [P0] The system shall provide purchase, supplier balance, expense, payment-method and profit-and-loss reports.

FR-144 [P0] The system shall allow date range and relevant category/product/supplier/payment filters.

FR-145 [P0] The system shall export supported reports to PDF and Excel (XLSX). Exported totals shall reconcile to the on-screen report.

FR-146 [P1] The system shall support CSV export for raw tabular data where useful.

FR-147 [P0] The system shall show an explicit empty state when no records match the selected filters; it shall not substitute sample or fabricated figures.

## 5.14 Settings and Configuration

Settings shall be editable by the single operator and validated before application.

FR-150 [P0] The operator shall configure outlet name, address, contact details, receipt footer and optional logo.

FR-151 [P0] The operator shall configure receipt paper width, printer selection, copies and whether to auto-print after checkout.

FR-152 [P0] The operator shall configure reorder levels, near-expiry warning days, default payment method and other clearly defined operational preferences.

FR-153 [P0] Tax rate and tax treatment shall remain unconfigured/disabled until confirmed by the outlet owner and relevant advisor; the software shall not invent a tax rate.

FR-154 [P1] The operator may configure optional local application lock/PIN. It shall not create multi-user roles or remote authentication.

FR-155 [P0] The system shall provide a settings export/import mechanism or include settings safely in backups.

## 5.15 Backup, Restore and Data Integrity

Backup and recovery are mandatory before live business use.

FR-160 [P0] The database shall be stored in the current Windows user’s LocalAppData folder, outside the application installation directory.

FR-161 [P0] The operator shall be able to create a manual backup to a chosen folder.

FR-162 [P1] The system should create a scheduled local backup at a configurable interval, with a retention policy.

FR-163 [P0] Backup creation shall use a SQLite-safe mechanism that produces a consistent snapshot while the application is open.

FR-164 [P0] Restore shall validate the selected backup, show its timestamp and create a safety backup of current data before replacing the active database.

FR-165 [P0] If restore fails, the application shall retain or restore the pre-restore database and report the failure.

FR-166 [P0] The system shall provide a database integrity check and report whether the database can be opened and validated.

FR-167 [P0] The application shall log unexpected termination/recovery events and detect incomplete database operations through SQLite transaction guarantees.

FR-168 [P0] Schema updates shall use EF Core migrations and a safe upgrade path; application updates must never overwrite or delete user business data.

## 5.16 Audit Trail and Data Administration

Auditability is required for stock and financial operations.

FR-170 [P0] The system shall record creation and modification timestamps for key master and transaction records.

FR-171 [P0] The system shall record audit events for completed sales, returns, voids, purchase receiving, supplier payments, expenses, stock adjustments, waste, recipe changes, settings changes and restore operations.

FR-172 [P0] An audit event shall include event type, affected record, timestamp, reason where applicable and before/after summary where appropriate.

FR-173 [P0] Completed sales, received purchases and posted stock movements shall not be physically deleted through normal UI workflows.

FR-174 [P1] The operator shall be able to view and export an audit history filtered by date, event type and record reference.

# 6. Business Rules

| ID | Rule |
| --- | --- |
| BR-001 | Only active products and active categories are available for new sales and recipe definitions. Historic records retain the original snapshots. |
| BR-002 | SKU is mandatory and unique. Barcode is optional and unique when present. |
| BR-003 | Use decimal for all money and measurable quantities; do not use binary floating point for business calculations. |
| BR-004 | A completed sale requires valid sale lines and payment lines whose sum equals the amount due. |
| BR-005 | A sale commit is atomic across header, lines, payments, allocations, stock movements and balances. |
| BR-006 | A prepared menu item is not stocked as a finished item unless explicitly configured; its recipe ingredients are consumed at sale. |
| BR-007 | A packaged product consumes its own stock batch quantity at sale. |
| BR-008 | Inventory balance changes must originate from a stock movement; UI code must not directly overwrite balances. |
| BR-009 | FEFO is preferred for batches with expiry dates; FIFO applies when expiry data is unavailable. The final policy is owner-approved. |
| BR-010 | Expired inventory is not eligible for normal sale. |
| BR-011 | Negative stock is disallowed by default. An override, if approved later, must be explicit and auditable. |
| BR-012 | Recipe quantities must be convertible to the ingredient’s base unit without mixing incompatible dimensions. |
| BR-013 | Recipe versions and sale-time product/price/cost snapshots preserve historical sale meaning. |
| BR-014 | Purchase stock is increased only when the purchase is received; draft and cancelled purchases do not affect stock. |
| BR-015 | Receiving a purchase is idempotent; repeated clicks cannot duplicate stock. |
| BR-016 | Supplier balance equals posted purchase liabilities minus recorded supplier payments and valid credits/returns. |
| BR-017 | Customer attachment is optional; release 1.0 does not require customer data for walk-in sales. |
| BR-018 | Completed financial records are reversed/adjusted, not silently hard-deleted. |
| BR-019 | Returns cannot exceed the remaining sale quantity/value and must link to the original sale. |
| BR-020 | Only returned items marked resaleable may be restored to saleable inventory. Prepared/consumed products are not automatically restocked. |
| BR-021 | Net sales, COGS, gross profit, operating expenses and cash flow are separate measures and must not be conflated. |
| BR-022 | Historic COGS uses saved cost allocation/snapshots, not the current catalogue cost. |
| BR-023 | Tax calculation is disabled until the actual business requirement and applicable rate are confirmed. |
| BR-024 | Local business data persists across application restarts and is not stored in the install folder. |
| BR-025 | Restore always creates a pre-restore safety backup and validates the selected file before activation. |


# 7. Data Requirements and Conceptual Model

## 7.1 Data Design Principles
- Use normalized relational tables with primary and foreign keys.
- Use decimal columns for prices, totals and quantities. Define precision/scale consistently and validate within the application.
- Use UTC or offset-aware timestamps internally where practical and display in Asia/Colombo local time.
- Use status fields and audit records instead of deleting completed transactions.
- Use product, price, description and unit snapshots on transaction lines.
- Use batch allocation records to support traceable COGS and expiry-aware issue.
- Use database constraints and transactions in addition to UI validation.
- Introduce production migrations and upgrade tests before live usage; the starter EnsureCreated database pattern is prototype-only.

## 7.2 Core Entities and Key Attributes

| Entity | Key attributes (illustrative; final schema may normalize further) |
| --- | --- |
| Category | CategoryId (PK), Name, Description, DisplayOrder, IsActive, CreatedAt, UpdatedAt. |
| Product | ProductId (PK), SKU, Barcode, Name, CategoryId (FK), ProductType, BaseUnit, IsSellable, IsStockTracked, CostPrice, SellingPrice, ReorderLevel, IsActive. |
| Supplier | SupplierId (PK), Name, ContactPerson, Phone, Email, Address, Notes, IsActive. |
| Customer | CustomerId (PK), Name, Phone, Email, Address, Notes, IsActive. Optional in POS. |
| Sale | SaleId (PK), ReceiptNumber, SoldAt, Status, CustomerId (nullable FK), Subtotal, DiscountTotal, TaxTotal (if enabled), GrandTotal, Notes. |
| SaleItem | SaleItemId (PK), SaleId (FK), ProductId (nullable/reference FK), SKU/name/unit snapshots, Quantity, UnitPrice, Discount, LineTotal, UnitCostSnapshot, RecipeVersionId (nullable). |
| Payment | PaymentId (PK), SaleId (FK), Method, Amount, PaidAt, Reference, Notes. |
| SaleReturn / ReturnItem | ReturnId (PK), original SaleId (FK), returned date, reason, refund total; line records contain SaleItemId, quantity, refund amount and restock decision. |
| InventoryBatch | BatchId (PK), ProductId (FK), BatchNumber, ReceivedAt, ManufactureDate, ExpiryDate, ReceivedQuantity, RemainingQuantity, UnitCost, SourcePurchaseItemId. |
| StockMovement | MovementId (PK), ProductId (FK), BatchId (nullable FK), MovementType, QuantityDelta, Unit, OccurredAt, SourceType, SourceId, Reason, Notes. |
| StockAllocation | AllocationId (PK), SaleItemId (FK), BatchId (FK), Quantity, UnitCost, ExtendedCost. |
| Purchase | PurchaseId (PK), SupplierId (FK), InvoiceNumber, PurchaseDate, Status, Subtotal, Discount, Total, AmountPaid, Balance. |
| PurchaseItem | PurchaseItemId (PK), PurchaseId (FK), ProductId (FK), Quantity, UnitCost, LineTotal, BatchNumber, ExpiryDate. |
| SupplierPayment | SupplierPaymentId (PK), SupplierId (FK), PurchaseId (nullable FK), Amount, Method, PaidAt, Reference. |
| Recipe | RecipeId (PK), MenuProductId (FK), Version, EffectiveFrom, IsActive, Notes. |
| RecipeItem | RecipeItemId (PK), RecipeId (FK), IngredientProductId (FK), Quantity, Unit, WastagePercent. |
| ExpenseCategory | ExpenseCategoryId (PK), Name, IsActive, DisplayOrder. |
| Expense | ExpenseId (PK), ExpenseCategoryId (FK), Date, Description, Amount, PaymentMethod, Reference, Status. |
| CashSession (P1) | CashSessionId (PK), OpenedAt, OpeningFloat, ClosedAt, ExpectedCash, CountedCash, Variance, Status. |
| CashMovement (P1) | CashMovementId (PK), CashSessionId (FK), type, amount, reason, date/time, source reference. |
| AppSetting | SettingKey (PK), Value, UpdatedAt. Store preferences, outlet profile and receipt configuration. |
| AuditLog | AuditLogId (PK), EventType, EntityType, EntityId, OccurredAt, Reason, ChangeSummary. |
| BackupHistory | BackupId (PK), FilePath, CreatedAt, FileSize, ValidationStatus, Notes. |


## 7.3 Entity Relationships
- One Category has many Products; a Product belongs to one active Category at a time.
- One Supplier has many Purchases and SupplierPayments. One Purchase has many PurchaseItems.
- One Sale has many SaleItems and Payments. A SaleItem may have many StockAllocations.
- One Product has many InventoryBatches and StockMovements. A batch may be referenced by many StockMovements and StockAllocations.
- One prepared/menu Product may have multiple Recipe versions. Each Recipe has many RecipeItems; each RecipeItem references an ingredient Product.
- One ExpenseCategory has many Expenses.
- A SaleReturn references one original Sale and contains one or more ReturnItems.
- AuditLog and BackupHistory are independent supporting records that reference business entities by type and identifier.

## 7.4 Data Retention and Deletion

Master data may be deactivated. Transactional data must be retained and linked. A separate owner-approved data retention policy is required before any bulk purge function is added. The application must not automatically delete business transactions, audit events or backups on a short default retention period.

# 8. External Interface Requirements

## 8.1 User Interface

The primary interface is a WPF desktop application using XAML and MVVM. Views shall not directly execute SQL or access the DbContext. Commands shall call application services through view models. Business rules must be enforced in the application/domain layer even if the UI already validates the same input.

## 8.2 Hardware Interfaces

| Interface | Requirement |
| --- | --- |
| Barcode scanner | Optional USB scanner using keyboard-wedge input. No proprietary driver integration is required for release 1.0. Scanner input must not accidentally type into unrelated fields. |
| Thermal printer | Optional Windows-installed 58 mm or 80 mm receipt printer. Provide printer selection, paper width and test-print action. |
| A4 printer | Optional standard Windows printer for reports and formal print views. |
| Cash drawer | No direct drawer kick integration required in release 1.0; may be added if the selected printer supports it. |


## 8.3 Software Interfaces
- SQLite database file accessed through Entity Framework Core in Infrastructure.
- PDF export library and XLSX export library, selected during implementation and maintained at compatible versions.
- Windows print subsystem for receipt/report printing.
- Local file system for backup, restore, logs and optional product images.

## 8.4 Communications Interfaces

No inbound or outbound network API is required for release 1.0. The application shall not expose a local web server or require cloud connectivity for sales, inventory, purchases, expenses or reports.

# 9. UI / UX Requirements

## 9.1 Visual Direction

Use a calm, modern retail-management visual language: deep Highland-inspired blue navigation, bright white work surfaces, soft cool-grey canvas and restrained semantic status colors. This is a proposed application identity and must not be represented as an official MILCO brand guideline unless the outlet supplies official brand assets.

| Token | Value | Use |
| --- | --- | --- |
| Brand blue | #1769AA | Primary actions, active navigation and key highlights. |
| Deep navigation | #102F46 | Sidebar and strong headings. |
| Canvas | #F4F7FB | Main app background. |
| Surface | #FFFFFF | Cards, forms and tables. |
| Primary text | #172B4D | Headings and body content. |
| Secondary text | #718096 | Hints, captions and metadata. |
| Border | #E7EDF4 | Dividers and control outlines. |
| Success | #16865B | Completed/success states; pair with text/icon. |
| Warning | #B7791F | Low stock/expiry warnings; pair with text/icon. |
| Error | #B83232 | Validation and failure states; pair with text/icon. |


## 9.2 Typography and Layout
- Use Segoe UI or Aptos with Windows-safe fallback.
- Sidebar target width approximately 248 px; top header approximately 84 px.
- Main content padding approximately 24–28 px.
- Use a spacing scale based on 4, 8, 12, 16, 20, 28 and 32 px.
- Cards use approximately 12–16 px corner radius.
- Buttons and input controls should normally be at least 38–42 px high; POS controls should be larger where practical.
- Primary information must remain readable at 1120×720 and 1366×768. Avoid horizontal overflow in common screens.

## 9.3 Required Screen Patterns

| Screen | Layout and interaction requirements |
| --- | --- |
| Dashboard | KPI cards; sales trend; top products; recent sales; low-stock and near-expiry alerts; quick actions; click-through filtering. |
| POS | Product/category search area and product grid on the left; persistent cart and payment summary on the right; large total and checkout action; keyboard-first operation. |
| Products | Search/filter toolbar; sortable data grid; add/edit form; active status; SKU/barcode validation; clear no-results state. |
| Inventory | Stock summary; batch/expiry filters; movement history; stock adjustment and waste forms with mandatory reason. |
| Milk Bar | Menu item list; recipe editor; ingredient availability; recipe costing; version history. |
| Purchases | Supplier and invoice header; line editor; draft/receive/cancel states; receiving confirmation; supplier balance. |
| Expenses | Date/category/amount entry; payment method; filtered expense list; edit/void trail. |
| Finance and reports | Date filters; summary cards; detailed tables/charts; clear definitions; export and print actions. |
| Settings | Grouped sections for outlet profile, receipt/printer, stock alerts, tax settings, backups and application preferences. |


## 9.4 Interaction and Feedback
- Use labels above fields, visible required markers, unit suffixes and examples where needed.
- Show validation beside the field and provide a concise summary for multi-error forms.
- Confirm destructive or irreversible actions; require reasons for voids and stock adjustments.
- Disable duplicate clicks during save/checkout; show progress only when an operation takes noticeable time.
- Provide useful empty states with a next action rather than a blank screen.
- Never rely on color alone to communicate status. Include label, icon or text.
- Preserve search/filter state when returning from a detail screen where practical.
- Show confirmation before closing the app if there is a draft transaction or unsaved change.

## 9.5 POS Usability Requirements
- The operator can reach product search without a mouse.
- The cart remains visible while products are added.
- The amount due, tendered amount, remaining amount and change are visually distinct.
- A failed checkout retains the cart so the operator can correct the issue; no partial records may be saved.
- Successful checkout clears the cart only after the database commit succeeds.
- Scanner input is routed to a dedicated search control when POS is active.

# 10. Non-Functional Requirements

| ID | Category | Requirement |
| --- | --- | --- |
| NFR-001 | Performance | On target hardware, common local screen navigation and search should complete within 1 second for a typical outlet dataset (up to 10,000 products and 100,000 sale lines), excluding printer response. |
| NFR-002 | Performance | A normal POS checkout should complete and show confirmation within 2 seconds under normal local database conditions, excluding slow external printer hardware. |
| NFR-003 | Reliability | Sales, returns, purchase receiving and stock adjustments shall use database transactions and rollback on failure. |
| NFR-004 | Data integrity | Use primary keys, foreign keys, uniqueness constraints and validation for all core entities. Enable appropriate SQLite foreign-key enforcement. |
| NFR-005 | Availability | Core application workflows shall work without internet after installation and dependency setup. |
| NFR-006 | Security | The database shall be stored within the current Windows user profile and not the public installation directory. OS account security is the primary access boundary. |
| NFR-007 | Security | Do not store payment card numbers, CVV, bank credentials or online banking passwords. Record only tender method, amount and optional non-sensitive reference. |
| NFR-008 | Privacy | Collect only customer/supplier details needed for outlet operation; provide deactivation and avoid unnecessary personal data. |
| NFR-009 | Maintainability | Keep Domain, Application, Infrastructure and WPF responsibilities separated; use dependency injection and MVVM. |
| NFR-010 | Maintainability | Business rules shall be testable without WPF views. Views shall not contain business logic or direct database access. |
| NFR-011 | Compatibility | The primary deployment target shall be Windows 11 64-bit. Windows 10 shall be supported only on editions/versions currently listed by Microsoft as compatible with .NET 10 and still receiving applicable security servicing; ordinary Windows 10 Home/Pro is not the production baseline. |
| NFR-012 | Usability | Common screens shall be keyboard accessible, readable and usable at 1120×720 or higher. |
| NFR-013 | Accessibility | Maintain meaningful focus order, visible focus indicator, accessible control names and adequate contrast. |
| NFR-014 | Backup | A valid backup shall be creatable without closing the application and restorable with a safety copy. |
| NFR-015 | Recovery | A failed backup/restore or database upgrade shall not silently destroy the currently active data file. |
| NFR-016 | Auditability | Inventory and financial transactions shall be traceable through movements, source references and audit records. |
| NFR-017 | Scalability | The data model should allow future outlets and users without claiming multi-outlet functionality in release 1.0. |
| NFR-018 | Portability | The solution shall build reproducibly from source using documented .NET SDK and NuGet dependency versions. |
| NFR-019 | Observability | Application logs shall capture exception details and operational failures without exposing private data unnecessarily. |
| NFR-020 | Localization | All date/time display shall use Asia/Colombo local time; all money shall use LKR formatting. |


# 11. Use Cases

## UC-01 Complete a Packaged Product Sale

| Element | Description |
| --- | --- |
| Primary actor | Outlet owner/operator. |
| Preconditions | Application is open; product is active/sellable; sufficient eligible stock exists. |
| Trigger | Operator selects POS and begins a sale. |
| Main flow | 1. Search/select packaged product. 2. Add quantity to cart. 3. Review price and discount. 4. Choose payment method(s). 5. Enter tender amounts. 6. System validates payment and stock. 7. Operator confirms checkout. 8. System atomically writes sale, lines, payment, allocations and stock movements. 9. System displays receipt and offers printing. |
| Alternate/failure | Insufficient stock: show available quantity and prevent checkout. Invalid payment: retain cart and return to payment panel. Database error: rollback all changes and retain cart. |
| Postconditions | One completed sale exists; stock and payments reconcile; receipt number is unique. |


## UC-02 Sell a Prepared Milk-Bar Item

| Element | Description |
| --- | --- |
| Preconditions | Prepared item is active, has an active recipe and required ingredient quantities are available. |
| Main flow | 1. Select menu item. 2. System displays price and optionally recipe availability. 3. Add item to cart. 4. Complete payment. 5. System resolves the recipe version, allocates ingredient batches using approved issue policy and atomically records sale plus ingredient movements. |
| Alternate/failure | Missing recipe, inactive ingredient or insufficient stock blocks checkout with a clear message. No sale or ingredient deduction is saved. |
| Postconditions | Sale line stores recipe/version and cost snapshots; ingredient stock is reduced traceably. |


## UC-03 Receive a Supplier Purchase

| Element | Description |
| --- | --- |
| Preconditions | Supplier and products exist; purchase is draft and quantities/costs are valid. |
| Main flow | 1. Create purchase. 2. Select supplier and invoice reference. 3. Add items, quantities, unit costs, batch/expiry details. 4. Record amount paid and payment method. 5. Review total and balance. 6. Confirm receiving. 7. System atomically creates inventory batches, stock movements, purchase status and payment records. |
| Alternate/failure | Invalid batch quantity, duplicate receive or database failure prevents duplicate stock; failed operation is rolled back. |
| Postconditions | Purchase is received once; stock and supplier payable balances are updated. |


## UC-04 Record Stock Adjustment or Waste

| Element | Description |
| --- | --- |
| Preconditions | Product and, when applicable, batch are known. |
| Main flow | 1. Open inventory. 2. Select product/batch. 3. Choose adjustment/waste/damage/expiry type. 4. Enter quantity and mandatory reason. 5. Review impact. 6. Confirm. 7. System creates stock movement and audit event. |
| Alternate/failure | Quantity exceeds allowed stock or unit is invalid: reject and explain. |
| Postconditions | Stock ledger reflects the adjustment and the history remains traceable. |


## UC-05 Process a Return

| Element | Description |
| --- | --- |
| Preconditions | Original sale exists and returned quantity is still eligible. |
| Main flow | 1. Search original receipt. 2. Select item and return quantity. 3. Choose refund method and restockability. 4. Enter reason. 5. Confirm. 6. System writes return records, refund and stock reversal if applicable. |
| Alternate/failure | Quantity/value exceeds remaining eligible amount: block. Prepared/consumed item: no automatic ingredient restock. |
| Postconditions | Original sale remains; return and refund are linked; net sales and stock are corrected. |


## UC-06 Record an Expense

| Element | Description |
| --- | --- |
| Preconditions | Expense category exists. |
| Main flow | 1. Open Expenses. 2. Enter date, category, description, amount and payment method. 3. Add reference/note if needed. 4. Save. 5. System records expense and audit data. |
| Postconditions | Expense is included in the relevant cash-flow and operating-result reports according to its payment date and classification. |


## UC-07 Generate a Financial Report

| Element | Description |
| --- | --- |
| Preconditions | Transaction data exists for selected period. |
| Main flow | 1. Open Finance/Reports. 2. Select report and date range. 3. Apply filters. 4. System calculates totals using approved definitions and historical cost data. 5. Operator reviews and exports PDF/XLSX. |
| Postconditions | Displayed and exported values match and show date range, outlet name and generation timestamp. |


## UC-08 Backup and Restore

| Element | Description |
| --- | --- |
| Backup flow | 1. Open Settings > Backup. 2. Select destination. 3. Start backup. 4. System creates SQLite-consistent snapshot, validates it, records backup metadata and reports success. |
| Restore flow | 1. Select backup file. 2. System validates integrity and displays backup time. 3. Operator confirms. 4. System creates safety backup. 5. System closes/replaces database safely and verifies it. 6. Application restarts/reopens using restored data. |
| Failure | If validation or restore fails, retain the active database or revert to the safety backup and display a clear error. |


# 12. Reports and Calculation Definitions

The report engine shall use consistent definitions. Any later accounting policy change must be documented and tested against historical data.

| Measure | Definition |
| --- | --- |
| Gross item sales | Sum of quantity × unit selling price before discounts for completed sale lines, excluding voided transactions. |
| Discounts | Sum of line and order discounts allocated to sale lines. Discount allocation must be deterministic for COGS/revenue analysis. |
| Net sales | Gross item sales minus discounts minus approved sales returns, excluding tax collected on behalf of a tax authority when tax is enabled. |
| Tax total | Separately calculated only when configured after owner confirmation. Tax must not silently be included as revenue. |
| COGS | Historical cost of packaged goods and recipe ingredients consumed for completed sales, less cost reversals for eligible returned/restocked goods. Based on batch allocations or saved sale-time cost snapshots. |
| Gross profit | Net sales minus COGS. |
| Operating expenses | Posted expense records classified as operating expenses for the selected period. |
| Net operating result | Gross profit minus operating expenses. It is a management measure and not a statutory profit figure. |
| Cash received | Payments recorded by tender method and payment date, less refunds recorded as outgoing payments. It is not identical to net sales where timing or non-cash tenders differ. |
| Purchase value | Value of purchase goods received, before/after discounts according to purchase report display. Purchase cost is not immediately an operating expense when goods remain in stock. |
| Supplier payable | Received purchase liabilities minus supplier payments and approved supplier credits/returns. |
| Stock valuation | Sum of remaining batch quantity × batch unit cost, using the selected costing policy. The method must be displayed. |
| Cash flow | Recorded cash inflows and outflows by their actual movement date. Sales, expenses, supplier payments and owner cash movements must be separately classified. |


## 12.1 Rounding and Precision
- Use decimal arithmetic for all currency, rates and quantities.
- Use a documented rounding mode (recommended: midpoint away from zero) at defined financial line/total boundaries; avoid repeated intermediate rounding.
- Persist final transaction line and header totals as saved snapshots.
- Use unit-specific precision for stock quantities. Display precision may differ from internal precision but shall not change stored values.
- All formulas shall have automated tests for discounts, split tender, returns, batch cost allocations, recipe conversions and profit reports.

## 12.2 Tax and Regulatory Configuration

No tax percentage, tax inclusion method, invoice legal wording or statutory report is specified in this SRS because the actual outlet treatment has not been provided. Tax fields must remain disabled or clearly marked not configured until the outlet owner confirms the requirements with an appropriate Sri Lankan tax/accounting advisor. The application must be configurable without hard-coding an assumed rate.

# 13. Acceptance Criteria

| ID | Acceptance criterion |
| --- | --- |
| AC-001 | Application installs and launches on the target Windows environment and opens the Dashboard without an internet connection after installation. |
| AC-002 | Closing and reopening the application preserves catalogue, settings, purchases, sales and expenses. |
| AC-003 | Product and category create/edit/deactivate/search workflows validate required fields and uniqueness. |
| AC-004 | A packaged sale with sufficient stock creates one sale, payment, receipt and correct stock deduction. |
| AC-005 | A sale with insufficient stock is blocked and creates no partial sale/payment/stock records. |
| AC-006 | A failed database save rolls back the entire checkout; retrying does not duplicate a sale. |
| AC-007 | A split payment is accepted only when tender totals equal the amount due; change is calculated correctly for cash. |
| AC-008 | A prepared item sale deducts all required recipe ingredients using the saved recipe version and correct batch allocation. |
| AC-009 | A recipe with missing/inactive ingredients or insufficient ingredient stock cannot be sold. |
| AC-010 | Purchase receiving increases stock once; repeated receive attempts do not duplicate quantities. |
| AC-011 | Batch/expiry views identify expired and near-expiry batches; FEFO/FIFO allocation is deterministic and traceable. |
| AC-012 | A partial return cannot exceed remaining sold quantity and reverses the correct amount and eligible stock. |
| AC-013 | Completed sales and posted stock movements cannot be hard-deleted through normal UI. |
| AC-014 | Expense and supplier payment records appear in the appropriate reports without misclassifying purchases as expenses. |
| AC-015 | Gross profit uses historical COGS and does not change merely because the current product cost was edited. |
| AC-016 | Daily/monthly reports reconcile to underlying sales, returns, costs, expenses and payment records. |
| AC-017 | PDF and XLSX exports match on-screen report values and include date range and generation timestamp. |
| AC-018 | A backup can be created while the application is running and passes integrity validation. |
| AC-019 | Restore creates a safety backup and can recover selected valid data; invalid backup files do not replace active data. |
| AC-020 | UI remains usable at 1120×720 and common POS tasks can be completed using keyboard input. |
| AC-021 | No product price, tax rate, outlet address or opening quantity is presented as official/sample live data without explicit operator entry. |
| AC-022 | Automated tests cover domain calculations, inventory allocation, transactional rollback and report reconciliation. |


# 14. Verification and Test Requirements

## 14.1 Test Levels

| Test level | Scope |
| --- | --- |
| Unit tests | Domain rules, price/discount calculations, payment reconciliation, recipe conversions, FEFO/FIFO selection, returns and profit calculations. |
| Application/service tests | Product validation, checkout orchestration, purchase receiving, expense posting, supplier payments, backup validation. |
| Persistence/integration tests | SQLite constraints, relationships, EF migrations, transaction rollback, duplicate receipt prevention and data recovery. |
| UI tests/manual checks | Navigation, validation messages, keyboard flow, responsive layout, printer selection and empty/error/success states. |
| End-to-end tests | Complete packaged sale, prepared-item sale, purchase receipt, return, cash close, report export and backup/restore. |
| Regression tests | Re-run critical transaction and financial test suites after every schema or business-rule change. |


## 14.2 Required Edge Cases
- Zero quantity, negative quantity, excessive decimal precision and invalid unit conversion.
- Duplicate SKU/barcode and archived category/product references.
- Expired batch, partly expired stock, multiple batches and mixed expiry/no-expiry lots.
- Multiple recipe ingredients with insufficient quantity in only one ingredient.
- Split payment mismatch by a small decimal amount; cash overpayment and change.
- Repeated checkout button click, app close during commit and database busy/locked state.
- Partial return followed by a second return; return beyond remaining sold quantity.
- Purchase receive repeated, purchase cancelled after draft, and invoice duplicate warning.
- Discount greater than line value, order discount allocation and refund after discounted sale.
- Backup during normal usage, invalid backup, low disk space and restore interruption.
- System date/time changes, month-end date filters and Asia/Colombo display.

## 14.3 Test Data

Use clearly labelled synthetic test data only. Do not seed realistic business figures as if they were actual outlet records. Starter catalogue names may be demonstrative, but prices and opening quantities must remain zero or unset until the operator enters real data.

# 15. Deployment, Backup and Support

## 15.1 Installation
- Provide a signed or clearly identified Windows installer when feasible.
- Installer shall create application shortcuts and support clean install/upgrade/uninstall.
- Business database and backups must remain outside the application binary directory.
- Uninstall shall not delete business data by default; show a separate explicit data-removal choice if such a function is ever provided.
- Document required runtime, printer setup, first-launch configuration and backup location.

## 15.2 Database Location

Default database location: %LOCALAPPDATA%\HighlandMilkBar\highland-milkbar.db. Logs and operational metadata may be stored in a neighbouring application data folder. Backups shall be stored in a configurable user-selected location, not only beside the active database.

## 15.3 Backup Policy Baseline

| Policy | Baseline recommendation |
| --- | --- |
| Manual backup | Available at all times from Settings. |
| Automatic backup | Once daily on first launch or before shutdown, subject to owner approval. |
| Retention | Keep a configurable rolling set; recommended initial default is 14 daily backups. |
| Validation | Verify SQLite integrity and backup metadata before marking as valid. |
| Restore | Require confirmation; create a safety backup; close active DB connection; replace safely; reopen and verify. |

> Note: The owner must copy important backups to a second physical device or secure external location. A backup stored only on the same PC does not protect against device loss or disk failure.

## 15.4 Support and Maintenance
- Provide a version number and release notes.
- Maintain a documented database migration path for every schema change.
- Provide a local diagnostic log export that avoids unnecessary personal data.
- Test upgrade from the previous production database before every release.
- Provide a documented recovery procedure for application failure and database restore.

# 16. Risks, Constraints and Open Decisions

## 16.1 Risks and Mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Incorrect opening stock or product cost | Reports and availability become unreliable. | Require opening-stock workflow, validation, clear operator data entry and reconciliation report. |
| Unconfirmed FEFO/FIFO policy | Wrong stock batch may be consumed. | Adopt FEFO for expiry-dated items and FIFO fallback as proposed; obtain owner approval before production. |
| Recipe/unit conversion mistakes | Ingredient balances and cost become inaccurate. | Use dimension-aware conversion, explicit base units and test cases; require recipe review. |
| Unsafe restore or schema upgrade | Business data loss. | Pre-restore safety copy, integrity test, migrations and upgrade/rollback tests. |
| Wrong tax assumptions | Receipt/compliance error. | Do not enable tax until owner/advisor provides the correct treatment. |
| Printer incompatibility | Receipt printing disruption. | Use Windows printer drivers, selectable paper width, preview/test print and manual receipt reprint. |
| SQLite file copied unsafely | Corrupt or inconsistent backup. | Use SQLite backup API or equivalent safe snapshot approach. |
| Starter features mistaken for production features | Operational losses. | Show clear release status and block live use until production acceptance tests pass. |


## 16.2 Open Decisions Requiring Owner Confirmation

| Decision | Proposed default | When required |
| --- | --- | --- |
| Stock allocation | FEFO for expiry-dated batches; FIFO otherwise. | Before inventory/POS production release. |
| Negative stock | Disabled. | Before POS release; default is safe. |
| Return policy | Packaged resaleable goods may be restocked; prepared/consumed goods are not automatically restocked. | Before returns release. |
| Tax handling | Disabled until confirmed; no assumed tax rate. | Before tax-enabled receipt/report release. |
| Receipt format | 80 mm thermal as preferred; 58 mm supported; A4 reports. | Before printer implementation. |
| Card/bank tender | Record tender only; external terminal handles actual payment. | Before POS payment release. |
| Customer credit | Out of scope in release 1.0. | Only if owner requests a scope change. |
| Cash session | Recommended P1 daily opening/closing cash reconciliation. | Before finance/daily closing release. |
| Backup schedule | Daily local backup; 14-day retention as proposed. | Before production deployment. |
| Owner/contact/legal receipt data | Supplied by outlet owner. | Before first live receipt. |


# 17. Delivery Roadmap

| Phase | Scope / exit condition |
| --- | --- |
| 0 – Starter foundation | Build/launch WPF shell, SQLite initialization, dashboard baseline and starter product list. Validate on target Windows PC. |
| 1 – Production foundation | EF Core migrations, dependency injection, application logging, validation framework, automated test project and safe database upgrade path. |
| 2 – Master data | Complete product/category CRUD, barcode support, units, supplier/customer management and settings. |
| 3 – Inventory | Opening stock, batch receiving, movements, FEFO/FIFO allocation, expiry alerts, adjustments, damage and stock reports. |
| 4 – POS | Product search/grid, cart, discounts, split payment, unique receipt, atomic checkout, printer workflow and sales history. |
| 5 – Returns | Full/partial return, refund, restock rules, void/reversal and audit records. |
| 6 – Milk bar | Menu items, recipe versioning, unit conversion, costing, ingredient availability and atomic consumption. |
| 7 – Procurement and suppliers | Purchase lifecycle, receiving, supplier payments, balances and reports. |
| 8 – Expenses and finance | Expense module, cash session, cash movement, financial definitions, reconciled P&L and cash-flow reports. |
| 9 – Reports and data safety | PDF/XLSX export, backup/restore UX, integrity check and operational diagnostics. |
| 10 – Production release | Installer, user acceptance testing, performance checks, data migration test, backup recovery drill and release documentation. |


## 17.1 Definition of Done for Each Feature
- Domain/business rule implemented and unit-tested.
- Application service implemented with validation and transaction handling.
- Persistence implemented with schema migration and constraints.
- View model and WPF view implemented using MVVM.
- Loading, empty, validation, success and failure states completed.
- Manual and automated tests pass; regression suite passes.
- Documentation and coding-agent handoff updated.

# Appendix A. Glossary

| Term | Definition |
| --- | --- |
| Batch / Lot | A quantity of a product received or manufactured together, potentially sharing expiry date and unit cost. |
| FEFO | First Expired, First Out: issue the batch with the earliest expiry date first. |
| FIFO | First In, First Out: issue the oldest received stock first. |
| COGS | Cost of Goods Sold: cost assigned to the products/ingredients consumed for completed sales. |
| POS | Point of Sale: the operator workflow for selecting items, taking payment and issuing a receipt. |
| Recipe | Ingredient list and quantities required to prepare one unit/serving of a menu item. |
| Stock movement | Traceable record of any increase/decrease or reclassification of inventory quantity. |
| Atomic transaction | A database operation in which all included changes succeed together or all are rolled back. |
| Snapshot | A copy of product name, price, cost or recipe details saved on a transaction so later edits do not rewrite history. |
| Single-user | One local operator on one PC; not a multi-user or concurrent networked deployment. |


# Appendix B. Initial Configuration Defaults

| Setting | Initial value / behaviour |
| --- | --- |
| Outlet name | “Highland Milk Bar” placeholder; operator must replace/confirm. |
| Currency | LKR. |
| Time zone | Asia/Colombo. |
| Language | English. |
| Database path | %LOCALAPPDATA%\HighlandMilkBar\highland-milkbar.db. |
| Negative stock | Disabled. |
| Batch issue rule | FEFO when expiry exists; FIFO fallback; owner approval pending. |
| Near-expiry warning | Suggested initial value: 7 days; editable by operator. |
| Reorder level | Configured per product; no assumed universal quantity. |
| Tax | Disabled until confirmed. |
| Payment methods | Cash, Card, Bank Transfer, Other. |
| Customer | Optional; anonymous walk-in supported. |
| Receipt | 80 mm preferred, 58 mm supported; printer selection required. |
| Automatic backup | Suggested once daily; owner approval pending. |
| Backup retention | Suggested 14 daily copies; editable. |
| Sample prices/stock | No live prices or opening stock are pre-populated. |


# Appendix C. Coding Agent Implementation Guidance
- Read this SRS together with README.md, PRODUCT_REQUIREMENTS.md, DESIGN_SYSTEM.md and CODING_AGENT_HANDOFF.md.
- Before adding modules, build and run Starter v0.1 on Windows and resolve existing compile/runtime issues.
- Preserve the solution layers: Domain, Application, Infrastructure and WPF. Use dependency injection and MVVM.
- Keep database access in Infrastructure/repositories/services; never query DbContext directly from XAML code-behind.
- Implement one vertical slice at a time: domain model/rules → application service → persistence/migration → view model → WPF view → automated tests.
- Use a transaction service for checkout, returns, purchase receiving and inventory adjustment. Include all related ledger, payment and batch changes in the same database transaction.
- Use stock movement records as the source of truth. Do not mutate stock balances directly in UI event handlers.
- Use immutable transaction snapshots and recipe versioning. Do not recalculate old sales from current catalogue prices or recipes.
- Replace EnsureCreated with EF Core migrations before production. Never delete or overwrite existing business data during upgrades.
- Do not implement tax, credit sales, remote sync or direct payment gateway integration without a confirmed change request.
- Update this SRS, README and handoff notes whenever an approved requirement changes.

# Appendix D. Technical Compatibility References

The following official Microsoft sources were checked on 14 November 2026 when setting the .NET and Windows compatibility baseline:
- .NET Support Policy – confirms .NET 10 is an active Long-Term Support release, with support shown through 14 November 2028: https://dotnet.microsoft.com/en-us/platform/support/policy
- Windows 10 support has ended – Microsoft states that general Windows 10 support ended on 14 October 2025 and that ordinary installations no longer receive standard security updates: https://support.microsoft.com/en-us/windows/deployment/updates-lifecycle/windows-10-support-has-ended-on-october-14-2025
- Install .NET on Windows – current .NET/Windows compatibility table, including the Windows 10 LTSC/Enterprise limitations for .NET 10: https://learn.microsoft.com/en-us/dotnet/core/install/windows
> End of SRS – Version 1.0