using System.Windows;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class CustomerDetailsWindow : Window
{
    private readonly CustomerDetailsViewModel _viewModel;

    public CustomerDetailsWindow(ICustomerQueryService query, ILoyaltyService loyalty, int customerId)
    {
        InitializeComponent();
        _viewModel = new CustomerDetailsViewModel(query, loyalty);
        DataContext = _viewModel;
        Loaded += async (_, _) =>
        {
            await _viewModel.LoadAsync(customerId);
            SubtitleText.Text = $"{_viewModel.CustomerCode} · {_viewModel.TypeText}";
        };
    }
}