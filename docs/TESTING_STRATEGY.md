# Testing Strategy

Step 15 QA approach for Highland Milk Bar Management System.

## Principles

- **Real SQLite, not InMemory.** Every integration test uses a temporary file database created by applying the real EF Core migrations (`TestDatabase`). This exercises relational constraints, transactions and SQL behaviour that InMemory cannot.
- **Regression first.** All tests from Steps 01–14 are retained. New tests are added only where a genuine coverage gap exists.
- **Measure before optimizing.** Performance work is driven by measured baselines, not guesses.
- **No production data in tests.** Destructive scenarios (restore, migration) run only against isolated temporary databases.

## Test layers

| Layer | Examples | Location |
| --- | --- | --- |
| Domain/service unit | Product validation, recipe costing, loyalty rules | `*ServiceTests.cs` |
| Integration (SQLite) | Purchase→inventory, checkout atomicity, backup/restore | `*IntegrationTests.cs`, `BackupRestoreTests.cs` |
| End-to-end workflow | Cross-module business flows | `EndToEndWorkflowTests.cs` |
| Performance baseline | Timed read paths + backup | `PerformanceBaselineTests.cs` |
| Migration compatibility | Fresh/legacy upgrade | `MigrationCompatibilityTests.cs` |

## Running

```
dotnet build ./HighlandMilkBar.sln
dotnet test  ./HighlandMilkBar.sln
```

## Current baseline

- **284 tests passing** (0 failed, 0 skipped) as of Step 15.
- Build: 0 warnings / 0 errors.

## Test environment

- Windows 11, .NET 10 SDK, `Debug` configuration, x64.
- Isolated temporary SQLite files under `%TEMP%`.
- Dataset for performance baseline: 2,000 products, 1,000 customers, 5,000 sales.

## Coverage focus

Financial calculations, checkout atomicity, inventory allocation/rollback, purchase receiving, returns, loyalty reversal, supplier payments, cash reconciliation, backup/restore recovery, report/export correctness, migration compatibility, and cross-module workflows.