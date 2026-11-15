# Database Optimization

Step 15 SQLite review.

## Findings

| Area | Status |
| --- | --- |
| Foreign keys | Enforced by the Microsoft.Data.Sqlite provider on connection open; `PRAGMA foreign_key_check` is also used by backup verification. |
| Indexes | Defined for common filter/join columns (SKU, barcode, `SoldAt`+status, `CustomerId`, `ProductId`, `SaleId`, `ExpiryDate`, `InvoiceNumber`, etc.). |
| Read tracking | Read/query services use `AsNoTracking` to avoid change-tracking overhead. |
| Pagination | List/search endpoints are paged (`PagedResult<T>`). |
| Parameterization | All data access is through EF Core LINQ; no string-concatenated or interpolated raw SQL. |
| Migrations | Hand-authored migrations + model snapshot; `DbInitializer` applies migrations idempotently and adopts legacy (EnsureCreated) databases safely. |
| WAL | The online backup API is WAL-correct (tested). Global WAL mode is not enabled; it is not required at the measured workload and was left unchanged to avoid an unmeasured change. |

## Integrity checks

`BackupVerificationService`/`BackupService` runs `PRAGMA integrity_check` and `PRAGMA foreign_key_check` on every backup. Tests assert:
- A valid backup passes both.
- A corrupted file fails integrity.
- An injected future migration is rejected (newer schema).

## Migration compatibility

Covered by `MigrationCompatibilityTests`:
- Fresh database initialization.
- Legacy database upgrade preserving existing customer/sale data.
- No use of `EnsureDeleted` or manual migration-history edits.

## Changes made

- **No schema/migration changes.** The review found indexes and constraints already present and adequate for the measured workload; adding indexes without a measured slow query would be unjustified.

## Recommendations (only if a real slow query appears)

- Add a filtered index on frequently filtered status/date combinations identified by measurement.
- Consider database-side `SUM` aggregation for financial summaries if sales volume grows well beyond the tested range.