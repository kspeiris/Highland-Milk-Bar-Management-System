using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Infrastructure.Services.Backup;

/// <summary>
/// Pure scheduling rules for automatic backups. Kept separate from the timer so the
/// rules can be unit-tested with controlled clocks without waiting on real time.
/// Local business time drives the schedule; UTC is used only for stored timestamps.
/// </summary>
internal static class BackupSchedule
{
    /// <summary>
    /// Determines whether an automatic backup is due at <paramref name="nowLocal"/>.
    /// Guarantees at most one run per scheduled period by comparing against the last
    /// successful backup. An overdue run is performed at most once (after which the
    /// last-success timestamp advances and the period is satisfied).
    /// </summary>
    public static bool IsDue(BackupSettingsDto settings, DateTime nowLocal)
    {
        if (!settings.AutomaticBackupEnabled || settings.Frequency == BackupFrequency.Disabled)
            return false;

        var scheduled = LastScheduledAt(settings, nowLocal);
        if (scheduled is null)
            return false;

        if (nowLocal < scheduled.Value)
            return false;

        // Already backed up at or after the current scheduled instant.
        if (settings.LastSuccessUtc is { } last && last.ToLocalTime() >= scheduled.Value)
            return false;

        return true;
    }

    /// <summary>
    /// The most recent scheduled instant (local) at or before <paramref name="nowLocal"/>,
    /// or null when the schedule cannot produce one.
    /// </summary>
    public static DateTime? LastScheduledAt(BackupSettingsDto settings, DateTime nowLocal)
    {
        if (settings.Frequency == BackupFrequency.Daily)
        {
            var today = nowLocal.Date + settings.BackupTime;
            return today <= nowLocal ? today : today.AddDays(-1);
        }

        if (settings.Frequency == BackupFrequency.Weekly)
        {
            // Walk back up to 6 days to the most recent matching weekday.
            for (var i = 0; i <= 6; i++)
            {
                var day = nowLocal.Date.AddDays(-i);
                if (day.DayOfWeek != settings.Weekday) continue;

                var candidate = day + settings.BackupTime;
                return candidate <= nowLocal ? candidate : null;
            }
        }

        return null;
    }

    /// <summary>Next scheduled instant (local) strictly after <paramref name="nowLocal"/>, for display.</summary>
    public static DateTime? NextRunAt(BackupSettingsDto settings, DateTime nowLocal)
    {
        if (!settings.AutomaticBackupEnabled || settings.Frequency == BackupFrequency.Disabled)
            return null;

        switch (settings.Frequency)
        {
            case BackupFrequency.Daily:
            {
                var today = nowLocal.Date + settings.BackupTime;
                return today > nowLocal ? today : today.AddDays(1);
            }
            case BackupFrequency.Weekly:
            {
                for (var i = 0; i <= 7; i++)
                {
                    var day = nowLocal.Date.AddDays(i);
                    if (day.DayOfWeek != settings.Weekday) continue;

                    var candidate = day + settings.BackupTime;
                    if (candidate > nowLocal) return candidate;
                }

                return null;
            }
            default:
                return null;
        }
    }
}