using System.Windows;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class ExpenseAnalyticsWindow : Window
{
    private readonly ExpenseAnalyticsViewModel _viewModel;

    public ExpenseAnalyticsWindow(IExpenseQueryService query)
    {
        InitializeComponent();
        _viewModel = new ExpenseAnalyticsViewModel(query);
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();

    private async void Apply_Click(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();
}