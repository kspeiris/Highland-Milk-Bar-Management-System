using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.Infrastructure.Services.Backup;

/// <summary>
/// Runs automatic backups while the application is open. An in-application scheduler can
/// only fire while the process is running, so a missed schedule is picked up as a single
/// overdue backup on the next start (see <see cref="BackupSchedule"/>). At most one backup
/// runs at a time; the timer ticks on a fixed interval rather than sleeping until the exact
/// scheduled second, which keeps the logic testable with a controlled clock.
/// </summary>
public sealed class BackupSchedulerService : IAsyncDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly IBackupService _backup;
    private readonly IBackupQueryService _query;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _singleFlight = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    public BackupSchedulerService(IBackupService backup, IBackupQueryService query, TimeProvider? clock = null)
    {
        _backup = backup;
        _query = query;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>True while the scheduler loop is running.</summary>
    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        _ = RunLoopAsync(_cts.Token);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        // Check immediately so an overdue backup runs at startup, then poll.
        await SafeTickAsync(cancellationToken);

        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await SafeTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task SafeTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            await TickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested.
        }
        catch
        {
            // A failed scheduled run must never take down the application; the failure is
            // logged by the caller-visible outcome and the last-success timestamp is not updated.
        }
    }

    /// <summary>
    /// Evaluates the schedule once. Returns true when a backup was attempted (successfully or
    /// not). Failures never advance the last-success timestamp.
    /// </summary>
    public async Task<bool> TickAsync(CancellationToken cancellationToken = default)
    {
        if (!await _singleFlight.WaitAsync(0, cancellationToken))
            return false; // Another tick is already running.

        try
        {
            var settings = await _query.GetSettingsAsync(cancellationToken);
            var nowLocal = _clock.GetLocalNow().LocalDateTime;

            if (!BackupSchedule.IsDue(settings, nowLocal))
                return false;

            settings.LastAttemptUtc = _clock.GetUtcNow().UtcDateTime;
            await _query.SaveSettingsAsync(settings, cancellationToken);

            try
            {
                var result = await _backup.CreateBackupAsync(BackupType.Automatic, settings.Location, cancellationToken);
                if (result.Success)
                {
                    settings.LastSuccessUtc = _clock.GetUtcNow().UtcDateTime;
                    await _query.SaveSettingsAsync(settings, cancellationToken);
                }

                await _query.RunRetentionAsync(settings.Location, cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Failure: leave LastSuccessUtc untouched and let the app keep running.
                try { await _query.RunRetentionAsync(settings.Location, CancellationToken.None); } catch { }
                return true;
            }
        }
        finally
        {
            _singleFlight.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        IsRunning = false;
        try { _cts.Cancel(); } catch { }
        _cts.Dispose();
        _singleFlight.Dispose();
        await ValueTask.CompletedTask;
    }
}