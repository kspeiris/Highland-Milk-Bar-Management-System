# Database Design — Highland Milk Bar Management System

**Version:** 1.0 (Step 02 – Database Foundation)
**Database:** SQLite (local, single-user, offline-first)
**ORM:** Entity Framework Core 10 (Code First + migrations)
**Architecture:** Clean Architecture (Domain / Application / Infrastructure / WPF)

---

## 1. Purpose

This document describes the relational database that underpins the whole application. The
database supports packaged product sales, ice cream and beverage sales, milk-bar recipes,
ingredient-level inventory, batch and expiry tracking, FIFO/FEFO allocation, purchases,
suppliers, customers, expenses, cash sessions, financial reporting, sales returns and
backup/restore.

The database is a **foundation**. POS screens, inventory UI and reports are built on top of
it in later steps.

---

## 2. Architecture and layering

| Layer | Responsibility | Database role |
|---|---|---|
| `HighlandMilkBar.Domain` | Entities, enums, invariants | Defines tables and relationships |
| `HighlandMilkBar.Application` | DTOs, service interfaces | Contracts only; no EF types |
| `HighlandMilkBar.Infrastructure` | EF Core `AppDbContext`, entity configurations, migrations, services | All persistence |
| `HighlandMilkBar.WPF` | Views / view models | Never touches `AppDbContext` directly |

Persistence rules:

- `AppDbContext` lives in `Infrastructure/Data`.
- Entity mapping lives in **separate `IEntityTypeConfiguration<T>` classes** under
  `Infrastructure/Data/Configurations` (not inside `AppDbContext`).
- Services (`InventoryService`, `PurchaseService`, `RecipeService`, `SalesService`,
  `BackupService`) implement the interfaces in `Application/Services`.
- View models depend on interfaces, never on `AppDbContext`.

---

## 3. Key design decisions

1. **Money is `decimal`, not floating point.** All monetary values use `decimal(18,2)` and
   all quantities use `decimal(18,3)`; unit costs use `decimal(18,4)`. `decimal` is exact for
   financial arithmetic and avoids floating-point rounding, satisfying the "never use
   floating-point for financial calculations" requirement. This is a deliberate, documented
   choice that preserves the starter's existing `decimal` model and the `CODING_AGENT_HANDOFF`
   rule ("use `decimal` for all monetary and quantity calculations"). See section 9 for the
   relationship to the "integer minor units" preference.

2. **Code First with migrations.** `EnsureCreated`/`EnsureDeleted` are no longer used at
   runtime. `DbInitializer` applies migrations with `Database.MigrateAsync()` and then seeds
   idempotent reference data. User data is never dropped or recreated.

3. **Historical integrity.** Sale lines store name/price/cost **snapshots**
   (`ItemNameSnapshot`, `UnitPrice`, `UnitCostSnapshot`). Changing a product price or recipe
   later never rewrites historic sales.

4. **No hard delete of completed transactions.** Sales, payments, purchases and returns use
   status fields and `Restrict` delete behaviour. Financial records are preserved.

5. **FEFO by default.** Perishable stock is consumed First-Expired-First-Out; FIFO is used as
   the tie-breaker/fallback. Policy is configurable per call (`AllocationPolicy`), defaulting
   to `Fefo` (also stored in `AppSettings`).

6. **Batch-level allocation is auditable.** Every consumption writes a `StockAllocation` row
   (which batch, how much) plus a `StockMovement` ledger row.

7. **Enums are stored as integers.** EF Core's default enum conversion is used for
   `ProductType`, `PaymentMethod`, `StockMovementType`, etc.

8. **SQLite online backup.** Backups use `SqliteConnection.BackupDatabase` (WAL-safe), never a
   raw file copy of a live database.

---

## 4. Domain overview (12 domains → tables)

| Domain | Tables |
|---|---|
| A. Product management | `Categories`, `Products`, `Units`, `ProductUnits` |
| B. Inventory management | `InventoryBatches`, `StockMovements`, `StockAdjustments`, `StockAdjustmentItems`, `StockAllocations` |
| C. Recipe / ingredient | `Recipes`, `RecipeItems` |
| D. Sales / POS | `Sales`, `SaleItems`, `Payments` |
| E. Sales returns | `SalesReturns`, `SalesReturnItems` |
| F. Purchase | `Purchases`, `PurchaseItems` |
| G. Supplier | `Suppliers`, `SupplierPayments` |
| H. Customer | `Customers` |
| I. Expense | `ExpenseCategories`, `Expenses` |
| J. Cash management | `CashSessions`, `CashMovements` |
| K. System | `AppSettings`, `AuditLogs` |

Total: **27 tables**.

---

## 5. Relationships and delete behaviour

| Relationship | Cardinality | Delete behaviour |
|---|---|---|
| Category → Product | 1 : many | Restrict |
| Product → InventoryBatch | 1 : many | Restrict |
| Product → ProductUnit | 1 : many | Cascade |
| Unit → ProductUnit | 1 : many | Restrict |
| Supplier → Purchase | 1 : many | SetNull |
| Supplier → InventoryBatch | 1 : many | SetNull |
| Purchase → PurchaseItem | 1 : many | Cascade |
| Purchase → SupplierPayment | 1 : many | Cascade |
| PurchaseItem → InventoryBatch | 1 : many | SetNull |
| Product → StockMovement | 1 : many | Restrict |
| InventoryBatch → StockMovement | 1 : many | SetNull |
| InventoryBatch → StockAllocation | 1 : many | Restrict |
| Sale → SaleItem | 1 : many | Cascade |
| Sale → Payment | 1 : many | Cascade |
| Sale → SalesReturn | 1 : many | Restrict |
| SaleItem → StockAllocation | 1 : many | Cascade |
| SaleItem → SalesReturnItem | 1 : many | SetNull |
| Customer → Sale | 1 : many | SetNull |
| Customer → SalesReturn | 1 : many | SetNull |
| Recipe → RecipeItem | 1 : many | Cascade |
| Product → Recipe (menu) | 1 : many | Restrict |
| RecipeItem → Product (ingredient) | many : 1 | Restrict |
| StockAdjustment → StockAdjustmentItem | 1 : many | Cascade |
| ExpenseCategory → Expense | 1 : many | SetNull |
| CashSession → CashMovement | 1 : many | Cascade |
| CashSession → Sale | 1 : many | SetNull |
| CashSession → Expense | 1 : many | SetNull |

`Restrict` is used wherever deleting a parent would destroy financial/history data.

---

## 6. Uniqueness, indexes and constraints

- **Unique:** `Categories.Name`, `Products.Sku`, `Units.Name`, `ProductUnits(ProductId,UnitId)`,
  `Sales.ReceiptNumber`, `SalesReturns.ReturnNumber`, `StockAdjustments.ReferenceNumber`,
  `ExpenseCategories.Name`, `AppSettings.Key`.
- **Indexes:** `Products.Barcode`, `Products(CategoryId,IsActive)`, `Sales(SoldAt,Status)`,
  `Purchases.InvoiceNumber`, `InventoryBatches(ProductId,BatchNumber)`, `InventoryBatches.ExpiryDate`,
  `StockMovements(ProductId,CreatedAt)`, `StockMovements.ReferenceType`,
  `StockAllocations(SaleItemId,InventoryBatchId)`, `Recipes(MenuProductId,Version)`,
  `Expenses.ExpenseDate`, `CashMovements(CashSessionId,CreatedAt)`, `AuditLogs.Timestamp`,
  `AuditLogs(EntityType,EntityId)`.
- **Precision:** money `decimal(18,2)`, quantity `decimal(18,3)`, unit cost `decimal(18,4)`.
- **Required fields:** SKU, product/category names, receipt/return numbers, batch numbers,
  adjustment reason, expense description, setting key.

---

## 7. Inventory model and FIFO/FEFO

`InventoryBatch` holds received quantity, remaining quantity, unit cost and expiry. All changes
are traceable:

```
Receive  -> new InventoryBatch + StockMovement(Purchase/Opening)  (+ Product.CurrentStock)
Consume  -> AllocationResult -> N StockMovements(Sale/RecipeConsumption)
                            -> N StockAllocations (per batch)      (- Product.CurrentStock)
Return   -> StockMovement(SaleReturn) back into the original batches
Adjust   -> StockMovement(AdjustmentIn/AdjustmentOut)
```

Allocation order (`InventoryService.AllocateAsync`):

- **FEFO:** earliest `ExpiryDate` first (batches with no expiry last), then `ReceivedAt`, then `Id`.
- **FIFO:** earliest `ReceivedAt` first, then `ExpiryDate`, then `Id`.
- Expired batches (`ExpiryDate < today`) are **never** allocated (DBR-03).
- Insufficient stock throws and rolls back (no partial deduction).

`Product.CurrentStock` is a cached aggregate kept consistent with batch balances by the service
layer; the ledger (`StockMovement`) is the source of truth.

---

## 8. Sales, recipes, purchases, finance

- **Checkout** (`SalesService.CheckoutAsync`) is one database transaction: validate → allocate
  batches → deduct inventory → consume recipe ingredients → write sale/items/payments/movements
  → commit. Any failure rolls back everything (DBR-06/07/08/09).
- **Recipes** (`RecipeService`) cost ingredients from current cost and expose availability;
  sale-time ingredient consumption uses the recipe's `YieldQuantity` to scale quantities.
- **Receiving a purchase** (`PurchaseService.ReceiveAsync`) creates batches and movements; drafts
  never change stock (DBR-10).
- **Finance:** sale COGS is captured from line cost snapshots; revenue ≠ profit. `CashSession` /
  `CashMovement` separate cash flow from sales totals.

---

## 9. Money representation vs. "integer minor units"

The Step 02 brief prefers integer minor currency units. This implementation uses `decimal`
with fixed precision instead, for two documented reasons:

1. It preserves the starter's existing `decimal` model and the `CODING_AGENT_HANDOFF` rule, so
   existing product/dashboard code and the WPF layer are not rewritten or broken.
2. `decimal` is exact for financial arithmetic (no floating point), so the core requirement
   ("never use floating-point for financial calculations") is met.

If integer minor units are later mandated, the change is localised: a value converter on the
money properties plus a display helper, without altering the table semantics.

---

## 10. Initialization, seeding, backup

- `DbInitializer.InitializeAsync` → `MigrateAsync` + idempotent seed of units, categories,
  products, expense categories and default settings. Safe to run on every startup.
- `BackupService` → `CreateBackupAsync`, `RestoreAsync` (with integrity check and automatic
  pre-restore safety backup), `VerifyIntegrityAsync` (`PRAGMA integrity_check`).

---

## 11. Testing

`tests/HighlandMilkBar.Tests` uses a real temporary SQLite database created by applying the
real migrations (not the InMemory provider). Coverage includes initialization/seeding, unique
constraints, batch receiving, FIFO, FEFO, expired-stock rejection, purchase receiving, recipe
consumption, split payments, transaction rollback, sales returns and backup/restore.

Run: `dotnet test`.