# Backup Verification

A backup is trusted only after it passes verification. `BackupService.VerifyBackupAsync` returns a detailed `BackupVerificationDto` with an individual status for every check (`Pass`, `Fail`, `NotAvailable`, `Unknown`) plus messages and violations.

## Checks

| Check | Method |
| --- | --- |
| File exists | `File.Exists` |
| File size | non-zero |
| SQLite integrity | `PRAGMA integrity_check` must return `ok` |
| Foreign keys | `PRAGMA foreign_key_check` must return no rows |
| SHA-256 checksum | recompute and compare to the manifest value (when present) |
| Schema compatibility | migration history vs. the current application's known migrations |

The backup is opened **read-only** so verification never modifies it.

## Overall result

`OverallValid` is true only when file-exists, size, integrity, foreign keys and schema compatibility all pass, and the checksum is either **Pass** or **NotAvailable** (legacy). A checksum **Fail** invalidates the backup.

## Compatibility

- **Same or older schema** — compatible; forward migrations are applied on restore.
- **Newer/unknown migration ids** — rejected; an older application must not restore a newer schema.
- **Legacy (no `__EFMigrationsHistory`)** — accepted when required tables are present; adopted by the existing startup migration path.
- **No manifest** — legacy; checksum is reported `NotAvailable`, never "verified".

## Reporting

The Backup & Restore workspace's **Verify** and **Details** actions open a window listing every check, the migration ids, and any foreign-key violations. Restore also runs the same verification and blocks the restore when it fails.