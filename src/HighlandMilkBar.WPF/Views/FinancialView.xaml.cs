using System.Windows;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class FinancialView : System.Windows.Controls.UserControl
{
    private FinancialViewModel? _viewModel;

    public FinancialView() => InitializeComponent();

    private async void FinancialView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null && DataContext is FinancialViewModel vm)
            _viewModel = vm;
        if (_viewModel is { } hub)
            await hub.LoadAsync();
    }
}