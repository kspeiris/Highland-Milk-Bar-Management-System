using System.Text;
using System.Windows;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.WPF.Views;

public partial class RestoreConfirmationWindow : Window
{
    public RestoreConfirmationWindow(RestorePreviewDto preview)
    {
        InitializeComponent();
        Populate(preview);
    }

    private void Populate(RestorePreviewDto preview)
    {
        var backup = preview.Backup;
        FileNameText.Text = backup?.FileName ?? "(unknown backup)";
        CreatedText.Text = backup is null ? string.Empty : $"Backup date: {backup.CreatedAt:dd MMMM yyyy HH:mm}";
        SizeText.Text = backup is null ? string.Empty : $"Size: {FormatSize(backup.SizeBytes)}";
        ChecksumText.Text = backup is null
            ? string.Empty
            : backup.IsLegacy ? "Checksum: legacy backup (no manifest)" : "Checksum: SHA-256 recorded in manifest";

        var v = preview.Verification;
        if (v is not null)
        {
            OverallText.Text = v.OverallValid ? "Result: VALID BACKUP" : "Result: INVALID BACKUP";
            OverallText.Foreground = v.OverallValid
                ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
                : (System.Windows.Media.Brush)FindResource("DangerBrush");

            var sb = new StringBuilder();
            sb.AppendLine($"File exists:            {Status(v.FileExistsCheck)}");
            sb.AppendLine($"SQLite integrity:       {Status(v.SqliteIntegrityCheck)}");
            sb.AppendLine($"Foreign keys:           {Status(v.ForeignKeyCheck)}");
            sb.AppendLine($"SHA-256 checksum:       {Status(v.ChecksumCheck)}");
            sb.AppendLine($"Schema compatibility:   {Status(v.SchemaCompatibilityCheck)}");
            ChecksText.Text = sb.ToString();
        }

        if (preview.Warnings.Count > 0)
        {
            WarningsHeader.Visibility = Visibility.Visible;
            WarningsCard.Visibility = Visibility.Visible;
            WarningsText.Text = string.Join(Environment.NewLine, preview.Warnings.Select(w => "• " + w));
        }

        if (preview.Errors.Count > 0)
        {
            ErrorsHeader.Visibility = Visibility.Visible;
            ErrorsCard.Visibility = Visibility.Visible;
            ErrorsText.Text = string.Join(Environment.NewLine, preview.Errors.Select(e => "• " + e));
        }

        RestoreButton.IsEnabled = preview.CanRestore;
    }

    private static string Status(BackupCheckStatus status) => status switch
    {
        BackupCheckStatus.Pass => "PASS",
        BackupCheckStatus.Fail => "FAIL",
        BackupCheckStatus.NotAvailable => "N/A",
        _ => "UNKNOWN"
    };

    private static string FormatSize(long bytes)
        => bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:N1} MB" : $"{bytes / 1024d:N0} KB";

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Restore_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}