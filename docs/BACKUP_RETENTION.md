# Backup Retention

Retention keeps the backup folder from growing without bound, but never risks data protection.

## Settings

| Setting | Default | Meaning |
| --- | --- | --- |
| Keep automatic backups | 14 | Newest N automatic backups kept. |
| Maximum backup age | 30 days | Automatic backups older than this (beyond the retention window) may be removed. |
| Minimum valid automatic backups | 3 | Floor that retention never goes below. |

All values are configurable in **Backup & Restore → Settings**.

## Rules

1. Only **automatic** backups are ever deleted automatically.
2. Manual and pre-restore backups are **never** deleted automatically (they are protected).
3. Retention keeps at least the greater of the retention count and the minimum valid count.
4. Deletion only happens inside the managed backup directory; unrelated files are never touched.
5. Each deletion removes the `.db` and its `.manifest.json`.
6. Failures are reported without affecting the application or the database.

## How it runs

- `RunRetentionAsync` is called after every successful automatic backup.
- The retention result (`Deleted`, `DeletedFiles`, `KeptProtected`, `Failures`) is available for logging/diagnostics.

## Testing

`Retention_DeletesOnlyEligibleAutomaticBackups` verifies that only eligible automatic backups beyond the retention count are deleted, and that manual and pre-restore backups are preserved.