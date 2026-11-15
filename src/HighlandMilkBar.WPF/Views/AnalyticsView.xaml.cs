using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class AnalyticsView : UserControl
{
    private AnalyticsViewModel? _viewModel;

    public AnalyticsView() => InitializeComponent();

    private async void AnalyticsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null && DataContext is AnalyticsViewModel vm)
            _viewModel = vm;
        if (_viewModel is { } hub)
            await hub.LoadAsync();
    }
}