using System.Windows;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class BackupRestoreView : System.Windows.Controls.UserControl
{
    public BackupRestoreView() => InitializeComponent();

    private BackupRestoreViewModel Vm => (BackupRestoreViewModel)DataContext;

    private void Restore_Click(object sender, RoutedEventArgs e) => _ = RestoreFlow();

    private async Task RestoreFlow()
    {
        var vm = Vm;
        if (vm.SelectedBackup is null)
        {
            MessageBox.Show("Select a backup from the history first.", "Restore", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = await vm.BuildRestorePreviewAsync(vm.SelectedBackup.Info.Path);
        var window = new RestoreConfirmationWindow(preview) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() != true)
            return;

        var result = await vm.RestoreSelectedAsync(vm.SelectedBackup.Info.Path);
        MessageBox.Show(
            result.Success ? "Restore completed successfully." : $"Restore failed: {result.ErrorMessage}",
            "Restore", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private void Verify_Click(object sender, RoutedEventArgs e) => _ = VerifyFlow();

    private async Task VerifyFlow()
    {
        var vm = Vm;
        if (vm.SelectedBackup is null)
        {
            MessageBox.Show("Select a backup to verify.", "Verify", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var verification = await vm.Backup.VerifyBackupAsync(vm.SelectedBackup.Info.Path);
        new BackupDetailsWindow(verification) { Owner = Window.GetWindow(this) }.ShowDialog();
        await vm.LoadAsync();
    }

    private void Details_Click(object sender, RoutedEventArgs e) => _ = VerifyFlow();

    private void Delete_Click(object sender, RoutedEventArgs e) => _ = DeleteFlow();

    private async Task DeleteFlow()
    {
        var vm = Vm;
        if (vm.SelectedBackup is null)
        {
            MessageBox.Show("Select a backup to delete.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (vm.SelectedBackup.IsProtected)
        {
            MessageBox.Show("Manual and pre-restore backups are protected and are never deleted automatically.",
                "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show($"Delete {vm.SelectedBackup.FileName}?", "Delete backup",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm == MessageBoxResult.Yes)
            await vm.DeleteSelectedAsync(vm.SelectedBackup);
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => _ = SettingsFlow();

    private async Task SettingsFlow()
    {
        var window = new BackupSettingsWindow(Vm.Query) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
            await Vm.LoadAsync();
    }
}