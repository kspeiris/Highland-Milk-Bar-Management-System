using System.Windows;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class BackupSettingsWindow : Window
{
    private readonly BackupSettingsViewModel _viewModel;

    public BackupSettingsWindow(IBackupQueryService query)
    {
        InitializeComponent();
        _viewModel = new BackupSettingsViewModel(query);
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose a backup location"
        };

        if (dialog.ShowDialog(this) == true)
            _viewModel.Location = dialog.FolderName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var saved = await _viewModel.SaveAsync();
        if (saved)
        {
            DialogResult = true;
            return;
        }

        ErrorText.Text = _viewModel.Error;
        ErrorText.Visibility = Visibility.Visible;
    }
}