using System.Windows;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class CustomerAnalyticsWindow : Window
{
    private readonly CustomerAnalyticsViewModel _viewModel;

    public CustomerAnalyticsWindow(ICustomerQueryService query, ILoyaltyService loyalty)
    {
        InitializeComponent();
        _viewModel = new CustomerAnalyticsViewModel(query, loyalty);
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await _viewModel.LoadAsync();

    private async void SaveLoyalty_Click(object sender, RoutedEventArgs e) => await _viewModel.SaveLoyaltyAsync();
}