# Automatic Backup

Automatic backups run on a schedule while the application is open. The in-app scheduler cannot fire while the process is closed.

## Frequencies

| Frequency | Behaviour |
| --- | --- |
| Daily | Once per day at the configured time. |
| Weekly | Once per week on the configured weekday and time. |
| Disabled | No automatic backups. Manual backups still work. |

## Scheduling rules

1. At most one backup runs at a time (single-flight guard).
2. At most one backup per scheduled period — the last successful time is persisted.
3. If the app starts after a scheduled time, one overdue backup runs, then the period is satisfied.
4. A failed run never updates the last-successful timestamp.
5. A failure never stops the application.
6. The scheduler is disposed cleanly on shutdown.

## Stored settings (`AppSettings`)

`backup.auto_enabled`, `backup.frequency`, `backup.time`, `backup.weekday`, `backup.location`, `backup.retention_count`, `backup.max_age_days`, `backup.min_valid`, `backup.last_success_utc`, `backup.last_attempt_utc`.

## Important limitation

**Automatic in-app scheduling only runs while the application is open.** If the app is closed at the scheduled time, the backup runs once on the next start (overdue handling). Background backups while the app is closed would require Windows Task Scheduler integration, which is out of scope for Step 14.

## Verification

`BackupSchedule` holds the pure rules and is unit-tested with a controlled clock (`MutableTimeProvider`) for daily, weekly, disabled, duplicate-prevention, overdue and failed-backup cases.