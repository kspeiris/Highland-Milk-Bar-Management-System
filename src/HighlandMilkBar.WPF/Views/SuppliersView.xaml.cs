using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class SuppliersView : System.Windows.Controls.UserControl
{
    private SuppliersViewModel? _viewModel;

    public SuppliersView() => InitializeComponent();

    private async void SuppliersView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null && DataContext is SuppliersViewModel vm)
        {
            _viewModel = vm;
            vm.NewRequested += OnNew;
            vm.EditRequested += OnEdit;
            vm.DetailsRequested += OnDetails;
            vm.StatementRequested += OnStatement;
        }
        if (_viewModel is { } hub)
            await hub.LoadAsync();
    }

    private async void OnNew()
    {
        if (_viewModel is not { } hub) return;
        var window = new SupplierEditorWindow();
        if (window.ShowDialog() == true && window.CreateResult is { } request)
        {
            try { await hub.CreateAsync(request); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Cannot save supplier", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }

    private async void OnEdit(SupplierRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var detail = await hub.SupplierService.GetByIdAsync(row.Id);
        if (detail is null) return;
        var window = new SupplierEditorWindow(detail);
        if (window.ShowDialog() == true && window.UpdateResult is { } request)
        {
            try { await hub.UpdateAsync(request); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Cannot save supplier", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }

    private async void OnDetails(SupplierRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var window = new SupplierDetailsWindow(hub.SupplierService, hub.PaymentService, hub.PurchaseService, row.Id);
        window.ShowDialog();
        await hub.LoadAsync();
    }

    private async void OnStatement(SupplierRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var window = new SupplierDetailsWindow(hub.SupplierService, hub.PaymentService, hub.PurchaseService, row.Id);
        window.ShowDialog();
        await hub.LoadAsync();
    }
}