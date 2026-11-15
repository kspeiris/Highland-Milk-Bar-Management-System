# Restore & Recovery Rules

Restore is the most safety-critical operation. It protects both the selected backup and the current database.

## Restore workflow

1. **Verify** the selected backup (integrity, foreign keys, checksum, schema). Reject if invalid.
2. Confirm the source is **not the active database**.
3. **Pre-restore safety backup**: create a `PreRestore` backup of the current database and **verify it**. Abort if it fails.
4. **Replace in place** via the SQLite online backup API (`source.BackupDatabase(live)`), which is connection- and WAL-safe — no raw file overwrite while connections are open.
5. **Integrity check** on the restored data; on failure, roll back from the safety backup.
6. **Apply supported forward migrations** (`MigrateAsync`) for older schemas; on failure, roll back.
7. **Final verification** that the application can use the database; on failure, roll back.
8. On success, report completion (the UI still recommends a restart).

## Safety rules honoured

- The source is verified before anything is touched.
- A pre-restore safety backup is created and verified first.
- The safety backup is **never** deleted by restore or retention.
- Replacement uses a staged/consistent copy through SQLite's backup API, not a raw overwrite.
- WAL/SHM are handled by the online backup API; stale side files are not left behind.
- On any failure the previous database is restored from the safety backup.
- Post-restore migration failure preserves the safety backup and does not report success.
- A successful method return is not treated as proof: the code re-opens and integrity-checks the database.

## Recovery from a failed restore

If replacement or migration fails, the safety backup remains on disk (folder `pre-restore`). The application continues to run on the restored-from-safety database. Manual recovery: restore the safety backup file through the same workflow.

## Critical scenario (tested)

1. Create a database with transactions, back it up.
2. Add more transactions.
3. Restore the earlier backup.
4. Reopen and query: earlier records present, newer records absent.
5. The pre-restore safety backup still contains the newer records.

This is covered by `Restore_RoundTripsData_AndKeepsSafetyBackup` and related tests using real temporary SQLite databases.