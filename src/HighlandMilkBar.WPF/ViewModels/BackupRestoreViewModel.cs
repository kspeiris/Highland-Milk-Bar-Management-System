using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Presentation wrapper for a single backup file shown in the history grid.</summary>
public sealed class BackupRowViewModel
{
    public BackupRowViewModel(BackupInfoDto info)
    {
        Info = info;
        FileName = info.FileName;
        TypeLabel = info.Type switch
        {
            BackupType.Automatic => "Automatic",
            BackupType.PreRestore => "Pre-Restore",
            _ => "Manual"
        };
        CreatedAtText = info.CreatedAt.ToString("dd MMM yyyy HH:mm");
        SizeText = FormatSize(info.SizeBytes);
        StatusText = info.IsLegacy ? "Legacy" : info.IntegrityStatus == BackupCheckStatus.Pass ? "Verified" : "Unknown";
        IsProtected = info.IsProtected;
    }

    public BackupInfoDto Info { get; }
    public string FileName { get; }
    public string TypeLabel { get; }
    public string CreatedAtText { get; }
    public string SizeText { get; }
    public string StatusText { get; }
    public bool IsProtected { get; }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024d / 1024d:N1} MB";
        if (bytes >= 1024) return $"{bytes / 1024d:N0} KB";
        return $"{bytes} B";
    }
}

/// <summary>
/// Backup &amp; Restore workspace view model: status cards, quick actions, history and
/// automatic-backup summary. Dialogs (restore confirmation, settings, details) are opened
/// by the view; this view model exposes the services and the data they need.
/// </summary>
public sealed class BackupRestoreViewModel : ObservableObject
{
    private readonly IBackupService _backup;
    private readonly IBackupQueryService _query;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private bool _hasError;
    private string _error = string.Empty;

    private string _statusText = "Not protected";
    private string _statusCaption = "Create your first backup";
    private string _lastBackupText = "—";
    private string _totalBackupsText = "0";
    private string _storageUsedText = "0 B";
    private string _locationText = "—";
    private string _nextAutoText = "—";
    private string _lastVerifyText = "—";
    private string _autoStatusText = "Disabled";
    private string _autoFrequencyText = "—";
    private string _autoRetentionText = "—";
    private string _autoSummary = "—";
    private BackupRowViewModel? _selectedBackup;

    public BackupRestoreViewModel(IBackupService backup, IBackupQueryService query)
    {
        _backup = backup;
        _query = query;
        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        CreateBackupCommand = new RelayCommand(_ => _ = CreateManualBackupAsync());
        OpenFolderCommand = new RelayCommand(_ => OpenBackupFolder());
    }

    public IBackupService Backup => _backup;
    public IBackupQueryService Query => _query;

    public ObservableCollection<BackupRowViewModel> Backups { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public BackupRowViewModel? SelectedBackup
    {
        get => _selectedBackup;
        set => SetProperty(ref _selectedBackup, value);
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string StatusCaption { get => _statusCaption; private set => SetProperty(ref _statusCaption, value); }
    public string LastBackupText { get => _lastBackupText; private set => SetProperty(ref _lastBackupText, value); }
    public string TotalBackupsText { get => _totalBackupsText; private set => SetProperty(ref _totalBackupsText, value); }
    public string StorageUsedText { get => _storageUsedText; private set => SetProperty(ref _storageUsedText, value); }
    public string LocationText { get => _locationText; private set => SetProperty(ref _locationText, value); }
    public string NextAutoText { get => _nextAutoText; private set => SetProperty(ref _nextAutoText, value); }
    public string LastVerifyText { get => _lastVerifyText; private set => SetProperty(ref _lastVerifyText, value); }
    public string AutoStatusText { get => _autoStatusText; private set => SetProperty(ref _autoStatusText, value); }
    public string AutoFrequencyText { get => _autoFrequencyText; private set => SetProperty(ref _autoFrequencyText, value); }
    public string AutoRetentionText { get => _autoRetentionText; private set => SetProperty(ref _autoRetentionText, value); }
    public string AutoSummary { get => _autoSummary; private set => SetProperty(ref _autoSummary, value); }

    public async Task LoadAsync()
    {
        IsBusy = true;
        HasError = false;
        try
        {
            var settings = await _query.GetSettingsAsync();
            var storage = await _query.GetStorageInfoAsync(settings.Location);
            var backups = await _query.ListBackupsAsync(settings.Location);

            Backups.Clear();
            foreach (var info in backups)
                Backups.Add(new BackupRowViewModel(info));

            TotalBackupsText = backups.Count.ToString();
            StorageUsedText = BackupRowViewModelSize(storage.TotalSizeBytes);
            LocationText = settings.Location;
            LastVerifyText = backups.Count == 0
                ? "—"
                : backups[0].IsLegacy ? "Legacy (no checksum)" : "Integrity verified";

            var latest = backups.Count > 0 ? backups[0] : null;
            if (latest is null)
            {
                StatusText = "Not protected";
                StatusCaption = "Create your first backup";
                LastBackupText = "—";
            }
            else
            {
                LastBackupText = latest.CreatedAt.ToString("dd MMM yyyy HH:mm");
                var age = DateTime.Now - latest.CreatedAt;
                if (latest.IsLegacy || latest.IntegrityStatus != BackupCheckStatus.Pass)
                {
                    StatusText = "Needs attention";
                    StatusCaption = "Verify your latest backup";
                }
                else if (age.TotalDays <= 1)
                {
                    StatusText = "Protected";
                    StatusCaption = "Latest backup verified recently";
                }
                else
                {
                    StatusText = "Protected";
                    StatusCaption = $"Last backup {age.Days} day(s) ago";
                }
            }

            AutoStatusText = settings.AutomaticBackupEnabled && settings.Frequency != BackupFrequency.Disabled ? "Enabled" : "Disabled";
            AutoFrequencyText = settings.Frequency switch
            {
                BackupFrequency.Daily => "Daily",
                BackupFrequency.Weekly => $"Weekly ({settings.Weekday})",
                _ => "—"
            };
            AutoRetentionText = $"Keep last {settings.RetentionCount} automatic backups";
            AutoSummary = $"Automatic backup: {AutoStatusText} · {AutoFrequencyText} · {AutoRetentionText}";
            NextAutoText = NextRunText(settings);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CreateManualBackupAsync()
    {
        IsBusy = true;
        HasError = false;
        try
        {
            var settings = await _query.GetSettingsAsync();
            var result = await _backup.CreateBackupAsync(BackupType.Manual, settings.Location);
            if (result.Success)
            {
                StatusMessage = $"Backup completed: {result.FileName} ({BackupRowViewModelSize(result.SizeBytes)}). Integrity verified, SHA-256 checksum recorded.";
                await LoadAsync();
            }
            else
            {
                Fail(new InvalidOperationException(result.ErrorMessage ?? "Backup failed."));
            }
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<RestorePreviewDto> BuildRestorePreviewAsync(string backupFilePath)
    {
        var verification = await _backup.VerifyBackupAsync(backupFilePath);
        var info = Backups.FirstOrDefault(b => string.Equals(b.Info.Path, backupFilePath, StringComparison.OrdinalIgnoreCase))?.Info
            ?? new BackupInfoDto { FileName = Path.GetFileName(backupFilePath), Path = backupFilePath };

        var preview = new RestorePreviewDto
        {
            CanRestore = verification.OverallValid,
            Backup = info,
            Verification = verification
        };

        if (!verification.OverallValid)
            preview.Errors.Add("This backup did not pass verification and cannot be restored.");
        preview.Warnings.Add("Records created after this backup will no longer appear in the restored database.");
        preview.Warnings.Add("A pre-restore safety backup of the current database will be created first.");
        return preview;
    }

    public async Task<BackupOperationResult> RestoreSelectedAsync(string backupFilePath)
    {
        IsBusy = true;
        HasError = false;
        try
        {
            var result = await _backup.RestoreBackupAsync(backupFilePath);
            if (result.Success)
                StatusMessage = "Restore completed. The database was restored and verified.";
            else
                Fail(new InvalidOperationException(result.ErrorMessage ?? "Restore failed."));
            await LoadAsync();
            return result;
        }
        catch (Exception ex)
        {
            Fail(ex);
            return new BackupOperationResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task DeleteSelectedAsync(BackupRowViewModel row)
    {
        if (row.IsProtected)
        {
            Fail(new InvalidOperationException("Manual and pre-restore backups are protected and cannot be deleted here."));
            return;
        }

        IsBusy = true;
        try
        {
            await _query.DeleteBackupAsync(row.Info.Path);
            StatusMessage = $"Deleted {row.FileName}.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenBackupFolder()
    {
        try
        {
            var settings = _query.GetSettingsAsync().GetAwaiter().GetResult();
            var folder = string.IsNullOrWhiteSpace(settings.Location)
                ? BackupDefaultsFolder()
                : settings.Location;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private static string BackupDefaultsFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HighlandMilkBar",
        "Backups");

    private static string NextRunText(BackupSettingsDto settings)
    {
        if (!settings.AutomaticBackupEnabled || settings.Frequency == BackupFrequency.Disabled)
            return "Automatic backup disabled";

        var now = DateTime.Now;
        DateTime next;
        if (settings.Frequency == BackupFrequency.Daily)
        {
            var today = now.Date + settings.BackupTime;
            next = today > now ? today : today.AddDays(1);
        }
        else
        {
            next = now.Date;
            for (var i = 0; i <= 7; i++)
            {
                var day = now.Date.AddDays(i);
                if (day.DayOfWeek != settings.Weekday) continue;
                var candidate = day + settings.BackupTime;
                if (candidate > now) { next = candidate; break; }
            }
        }

        return next.ToString("ddd dd MMM yyyy HH:mm");
    }

    private static string BackupRowViewModelSize(long bytes)
        => bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:N1} MB" : $"{bytes / 1024d:N0} KB";

    private void Fail(Exception ex)
    {
        HasError = true;
        Error = ex.Message;
        StatusMessage = string.Empty;
    }
}