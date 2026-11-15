using System.Text;
using System.Windows;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.WPF.Views;

public partial class BackupDetailsWindow : Window
{
    public BackupDetailsWindow(BackupVerificationDto verification)
    {
        InitializeComponent();
        Populate(verification);
    }

    private void Populate(BackupVerificationDto v)
    {
        SubtitleText.Text = v.FileName;
        OverallText.Text = v.OverallValid ? "Result: VALID BACKUP" : "Result: INVALID BACKUP";
        OverallText.Foreground = v.OverallValid
            ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
            : (System.Windows.Media.Brush)FindResource("DangerBrush");

        var sb = new StringBuilder();
        sb.AppendLine($"File exists:            {Status(v.FileExistsCheck)}");
        sb.AppendLine($"File size:              {Status(v.FileSizeCheck)}  ({v.FileSizeBytes:N0} bytes)");
        sb.AppendLine($"SQLite integrity:       {Status(v.SqliteIntegrityCheck)}");
        sb.AppendLine($"Foreign keys:           {Status(v.ForeignKeyCheck)}");
        sb.AppendLine($"SHA-256 checksum:       {Status(v.ChecksumCheck)}");
        sb.AppendLine($"Schema compatibility:   {Status(v.SchemaCompatibilityCheck)}");
        sb.AppendLine();
        sb.AppendLine(v.IsLegacy ? "Manifest:               none (legacy backup)" : "Manifest:               present");
        if (v.MigrationIds.Count > 0)
            sb.AppendLine($"Migrations:             {string.Join(", ", v.MigrationIds)}");
        ChecksText.Text = sb.ToString();

        if (v.ForeignKeyViolations.Count > 0)
        {
            var fk = new StringBuilder();
            fk.AppendLine("Foreign-key violations:");
            foreach (var violation in v.ForeignKeyViolations)
                fk.AppendLine("  " + violation);
            v.Messages.Add(fk.ToString());
        }

        if (v.Messages.Count > 0)
        {
            MessagesHeader.Visibility = Visibility.Visible;
            MessagesCard.Visibility = Visibility.Visible;
            MessagesText.Text = string.Join(Environment.NewLine + Environment.NewLine, v.Messages);
        }
    }

    private static string Status(BackupCheckStatus status) => status switch
    {
        BackupCheckStatus.Pass => "PASS",
        BackupCheckStatus.Fail => "FAIL",
        BackupCheckStatus.NotAvailable => "N/A",
        _ => "UNKNOWN"
    };

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}