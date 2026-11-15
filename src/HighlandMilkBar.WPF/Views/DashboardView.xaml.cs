using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class DashboardView : UserControl
{
    private DashboardViewModel? _viewModel;

    public DashboardView()
    {
        InitializeComponent();
    }

    private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null && DataContext is DashboardViewModel vm)
            _viewModel = vm;
        if (_viewModel is { } hub)
            await hub.LoadAsync();
    }
}