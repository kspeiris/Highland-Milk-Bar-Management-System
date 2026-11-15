# Regression Testing

Step 15 regression results for the existing Steps 01–14 functionality.

## Method

The full automated suite is run after every change. All pre-existing tests were retained unchanged except for cosmetic assertion fixes (collection-size assertions) and the addition of new workflow/performance tests.

## Result

- **284 passed, 0 failed, 0 skipped** (baseline was 278; +5 end-to-end workflow tests, +1 performance baseline).
- Build: 0 warnings / 0 errors.

## Modules covered by regression tests

| Module | Representative tests |
| --- | --- |
| Products & Categories | `ProductServiceTests`, `CategoryServiceTests` |
| Inventory | `InventoryTests`, batch/FIFO/FEFO/expiry |
| POS & Billing | `SalesServiceTests` (checkout, split payments, rollback) |
| Milk Bar | `RecipeCostingTests`, `RecipeAvailabilityTests`, `UnitConversionTests` |
| Purchases & Suppliers | `PurchaseServiceTests`, `PurchaseInventoryIntegrationTests`, `SupplierPaymentServiceTests` |
| Customers & Loyalty | `CustomerServiceTests`, `LoyaltyServiceTests`, `CustomerQueryServiceTests` |
| Expenses | `ExpenseServiceTests` |
| Finance | `FinancialQueryServiceTests`, `PurchaseFinancialsTests` |
| Dashboard & Analytics | `BusinessAnalyticsQueryServiceTests` |
| Reports | `ReportServicesTests` |
| Backup & Restore | `BackupRestoreTests` |
| Migrations | `MigrationCompatibilityTests` |

## New in Step 15

- `EndToEndWorkflowTests`: five cross-module business workflows (packaged sale, milk-bar sale, loyalty, purchase/supplier payments, expense/cash).
- `PerformanceBaselineTests`: timed read paths and backup over a generated dataset.

## Critical invariants re-confirmed

- Checkout rolls back stock and payments on failure.
- Purchase receiving cannot be applied twice.
- Loyalty points reverse on returns; no duplicate awards.
- Supplier overpayment is rejected; outstanding balances are correct.
- Expenses do not touch sales/inventory; cash movements reconcile.
- Financial reports are read-only and match the Finance workspace.
- Backup creation and verification do not modify business records.