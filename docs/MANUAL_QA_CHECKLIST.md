# Manual QA Checklist

Repeatable manual acceptance checklist for Step 15. Run on a build of the app; tick each item.

## Launch & navigation

- [ ] Application launches with no startup error (`%LOCALAPPDATA%\HighlandMilkBar\startup.log` absent or unchanged).
- [ ] Every sidebar item navigates: Dashboard, Analytics, POS, Sales History, Products, Inventory, Milk Bar, Purchases, Suppliers, Customers, Expenses, Finance, Reports, Backup, Settings.
- [ ] Sidebar collapse/expand works.

## Core modules

- [ ] Products: create, edit, search, activate/deactivate.
- [ ] Inventory: stock overview, batches, movements.
- [ ] POS: add/remove items, discount, cash/card/split payment, checkout, receipt.
- [ ] Sales History: search, open details, process a return.
- [ ] Milk Bar: recipes, ingredient availability, menu items.
- [ ] Purchases: create, receive, record supplier payment.
- [ ] Suppliers: directory, statement.
- [ ] Customers: create, history, loyalty balance.
- [ ] Expenses: create with cash session, list.
- [ ] Finance: P&L, cash flow, payables, reconciliation, payment analysis.
- [ ] Reports: generate a report, preview, export CSV/Excel/PDF, print.
- [ ] Backup: create backup, verify (details window), history, retention note, settings.

## Backup & restore (critical)

- [ ] Create a manual backup; confirm success message and manifest.
- [ ] Verify the backup; all checks PASS (or N/A for legacy).
- [ ] Restore an older backup; confirm the confirmation window shows warnings and the Restore button is enabled.
- [ ] After restore, reopen the app; data matches the restored backup.
- [ ] Confirm a pre-restore backup exists and is verified.

## Error handling

- [ ] Invalid input shows a clear message and does not crash.
- [ ] Restore of a corrupt/invalid file is rejected before any change.
- [ ] App keeps running after a failed backup.

## Note

Automated tests cover the business logic and backup/restore mechanics; this checklist covers the WPF presentation layer, which is not unit-tested.