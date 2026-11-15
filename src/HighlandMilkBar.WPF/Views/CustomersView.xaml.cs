using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class CustomersView : System.Windows.Controls.UserControl
{
    private CustomersViewModel? _viewModel;

    public CustomersView() => InitializeComponent();

    private async void CustomersView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null && DataContext is CustomersViewModel vm)
        {
            _viewModel = vm;
            vm.NewRequested += OnNew;
            vm.EditRequested += OnEdit;
            vm.DetailsRequested += OnDetails;
            vm.AnalyticsRequested += OnAnalytics;
        }
        if (_viewModel is { } hub)
            await hub.LoadAsync();
    }

    private void OnNew()
    {
        if (_viewModel is not { } hub) return;
        var window = new CustomerEditorWindow();
        if (window.ShowDialog() == true && window.CreateResult is { } request)
        {
            _ = SaveNew(hub, request);
        }
    }

    private async System.Threading.Tasks.Task SaveNew(CustomersViewModel hub, CustomerCreateRequest request)
    {
        try
        {
            await hub.CustomerService.CreateAsync(request);
            await hub.LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot save customer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnEdit(CustomerListRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var profile = await hub.QueryService.GetProfileAsync(row.Id);
        if (profile is null) return;
        var window = new CustomerEditorWindow(profile);
        if (window.ShowDialog() == true && window.UpdateResult is { } request)
        {
            try
            {
                await hub.CustomerService.UpdateAsync(request);
                await hub.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot save customer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void OnDetails(CustomerListRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var window = new CustomerDetailsWindow(hub.QueryService, hub.LoyaltyService, row.Id);
        window.ShowDialog();
        _ = hub.LoadAsync();
    }

    private void OnAnalytics()
    {
        if (_viewModel is not { } hub) return;
        var window = new CustomerAnalyticsWindow(hub.QueryService, hub.LoyaltyService);
        window.ShowDialog();
        _ = hub.LoadAsync();
    }
}