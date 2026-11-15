# Backup & Restore

Reliable data protection for the Highland Milk Bar database, introduced in **Step 14**. It extends the STEP 02 `BackupService` — it is not a new engine. Every backup is produced through SQLite's online backup API, is not reported successful until it is verified, and restore always creates and verifies a pre-restore safety backup first.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `DTOs/Backup/BackupDtos.cs` | `BackupInfoDto`, `BackupManifestDto`, `BackupVerificationDto`, `BackupSettingsDto`, `BackupOperationResult`, `RestorePreviewDto`, enums. |
| Application | `IBackupService`, `IBackupQueryService` | Create/verify/restore and history/settings/retention contracts. |
| Infrastructure | `Services/BackupService.cs` | Backup creation, verification, safe restore, history, storage, settings, retention. |
| Infrastructure | `Services/Backup/BackupManifest.cs` | Sidecar manifest read/write + SHA-256. |
| Infrastructure | `Services/Backup/BackupSchedule.cs` | Pure schedule rules (daily/weekly/overdue). |
| Infrastructure | `Services/Backup/BackupSchedulerService.cs` | Background scheduler while the app is open. |
| WPF | `BackupRestoreViewModel`, `BackupSettingsViewModel`, views | Backup & Restore workspace, settings, restore confirmation, details. |

## Backup pipeline (manual / automatic / pre-restore)

1. Validate/create destination.
2. Write to `<name>.db.partial`.
3. SQLite online backup from the live connection (WAL-safe, no file copy).
4. `PRAGMA integrity_check` + `PRAGMA foreign_key_check` on the temp file.
5. Compute SHA-256, atomically rename `.partial` → `.db`.
6. Write `<file>.db.manifest.json` (temp then atomic rename).
7. Report success only after verification.

A partially written backup never appears as complete (`*.partial` is excluded from history).

## Filename convention

`HighlandMilkBar_<Type>_<yyyyMMdd_HHmmss>.db` where type is `Manual`, `Auto` or `PreRestore`. Collisions get a numeric suffix; existing backups are never overwritten.

## Manifest

CamelCase JSON sidecar next to each backup: `backupFile`, `backupType`, `createdAtUtc`, `applicationVersion`, `schemaVersion`, `databaseSizeBytes`, `checksumAlgorithm`, `checksum`, `integrityCheck`, `foreignKeyCheck`, `operationId`. Legacy backups without a manifest are still verifiable; their checksum is reported as **N/A**, never as verified.

## Storage & settings

Default location `%LOCALAPPDATA%\HighlandMilkBar\Backups`; the user can choose any accessible folder (local, external, network). Settings persist in the existing `AppSettings` key/value table — no migration. See `AUTOMATIC_BACKUP.md` and `BACKUP_RETENTION.md`.

## Safety summary

- Backup creation never mutates business records.
- Restore verifies the source, then creates + verifies a pre-restore safety backup, then replaces in place and re-verifies (see `RESTORE_RECOVERY_RULES.md`).
- Retention never deletes manual or pre-restore backups (see `BACKUP_RETENTION.md`).

## Known limitations

- Automatic backups only run while the application is open (see `AUTOMATIC_BACKUP.md`).
- No cloud/off-site backup is implemented.
- Printing is not part of this module.