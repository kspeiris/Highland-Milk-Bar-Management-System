using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Edits and persists the automatic-backup and retention settings.</summary>
public sealed class BackupSettingsViewModel : ObservableObject
{
    private readonly IBackupQueryService _query;
    private bool _automaticBackupEnabled;
    private string _frequency = nameof(BackupFrequency.Daily);
    private string _backupTime = "22:00";
    private string _weekday = nameof(DayOfWeek.Sunday);
    private string _location = string.Empty;
    private int _retentionCount = 14;
    private int _maxBackupAgeDays = 30;
    private int _minimumValidBackups = 3;
    private bool _hasError;
    private string _error = string.Empty;

    public BackupSettingsViewModel(IBackupQueryService query) => _query = query;

    public ObservableCollection<string> FrequencyOptions { get; } = new()
    {
        nameof(BackupFrequency.Daily),
        nameof(BackupFrequency.Weekly)
    };

    public ObservableCollection<string> WeekdayOptions { get; } = new(
        Enum.GetNames<DayOfWeek>());

    public bool AutomaticBackupEnabled { get => _automaticBackupEnabled; set => SetProperty(ref _automaticBackupEnabled, value); }
    public string Frequency { get => _frequency; set => SetProperty(ref _frequency, value); }
    public string BackupTime { get => _backupTime; set => SetProperty(ref _backupTime, value); }
    public string Weekday { get => _weekday; set => SetProperty(ref _weekday, value); }
    public string Location { get => _location; set => SetProperty(ref _location, value); }
    public int RetentionCount { get => _retentionCount; set => SetProperty(ref _retentionCount, value); }
    public int MaxBackupAgeDays { get => _maxBackupAgeDays; set => SetProperty(ref _maxBackupAgeDays, value); }
    public int MinimumValidBackups { get => _minimumValidBackups; set => SetProperty(ref _minimumValidBackups, value); }
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }

    public async Task LoadAsync()
    {
        var settings = await _query.GetSettingsAsync();
        AutomaticBackupEnabled = settings.AutomaticBackupEnabled;
        Frequency = settings.Frequency == BackupFrequency.Weekly ? nameof(BackupFrequency.Weekly) : nameof(BackupFrequency.Daily);
        BackupTime = settings.BackupTime.ToString(@"hh\:mm");
        Weekday = settings.Weekday.ToString();
        Location = settings.Location;
        RetentionCount = settings.RetentionCount;
        MaxBackupAgeDays = settings.MaxBackupAgeDays;
        MinimumValidBackups = settings.MinimumValidBackups;
    }

    /// <summary>Validates and persists. Returns true when saved.</summary>
    public async Task<bool> SaveAsync()
    {
        HasError = false;

        if (!TimeSpan.TryParseExact(BackupTime?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var time)
            && !TimeSpan.TryParseExact(BackupTime?.Trim(), @"hh\:mm\:ss", CultureInfo.InvariantCulture, out time))
        {
            return Fail("Enter the backup time as HH:mm (24-hour), for example 22:00.");
        }

        if (string.IsNullOrWhiteSpace(Location))
            return Fail("Choose a backup location.");

        try
        {
            Directory.CreateDirectory(Location);
            var probe = Path.Combine(Location, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            return Fail($"The backup location is not usable: {ex.Message}");
        }

        if (RetentionCount < 1) return Fail("Retention must keep at least one automatic backup.");
        if (MinimumValidBackups < 1) return Fail("Minimum valid backups must be at least one.");
        if (MaxBackupAgeDays < 0) return Fail("Maximum backup age cannot be negative.");

        var settings = new BackupSettingsDto
        {
            AutomaticBackupEnabled = AutomaticBackupEnabled,
            Frequency = Frequency == nameof(BackupFrequency.Weekly) ? BackupFrequency.Weekly : BackupFrequency.Daily,
            BackupTime = time,
            Weekday = Enum.TryParse<DayOfWeek>(Weekday, true, out var w) ? w : DayOfWeek.Sunday,
            Location = Location.Trim(),
            RetentionCount = RetentionCount,
            MaxBackupAgeDays = MaxBackupAgeDays,
            MinimumValidBackups = MinimumValidBackups
        };

        await _query.SaveSettingsAsync(settings);
        return true;
    }

    private bool Fail(string message)
    {
        HasError = true;
        Error = message;
        return false;
    }
}