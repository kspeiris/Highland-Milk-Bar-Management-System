using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class PurchasesView : System.Windows.Controls.UserControl
{
    private PurchasesViewModel? _viewModel;

    public PurchasesView() => InitializeComponent();

    private async void PurchasesView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null && DataContext is PurchasesViewModel vm)
        {
            _viewModel = vm;
            vm.NewRequested += OnNew;
            vm.EditRequested += OnEdit;
            vm.ReceiveRequested += OnReceive;
            vm.CancelRequested += OnCancel;
            vm.DetailsRequested += OnDetails;
        }
        if (_viewModel is { } hub)
            await hub.LoadAsync();
    }

    private async void OnNew()
    {
        if (_viewModel is not { } hub) return;
        var editor = new PurchaseEditorViewModel(hub.PurchaseService, hub.SupplierService, hub.ProductService);
        var window = new PurchaseEditorWindow(editor);
        if (window.ShowDialog() == true)
            await hub.LoadAsync();
    }

    private async void OnEdit(PurchaseRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var detail = await hub.PurchaseService.GetByIdAsync(row.Id);
        if (detail is null) return;
        var editor = new PurchaseEditorViewModel(hub.PurchaseService, hub.SupplierService, hub.ProductService, detail);
        var window = new PurchaseEditorWindow(editor);
        if (window.ShowDialog() == true)
            await hub.LoadAsync();
    }

    private async void OnReceive(PurchaseRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var confirm = MessageBox.Show(
            $"Receive purchase {row.PurchaseNumber}? This creates inventory batches and stock movements and cannot be undone.",
            "Receive purchase", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;
        try
        {
            var result = await hub.ReceiveAsync(row.Id);
            MessageBox.Show($"Received {result.TotalQuantityReceived:0.##} units into {result.BatchesCreated} batch(es).",
                "Received", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot receive purchase", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnCancel(PurchaseRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var confirm = MessageBox.Show(
            $"Cancel purchase {row.PurchaseNumber}? Cancelled purchases never affect inventory or balances.",
            "Cancel purchase", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;
        try
        {
            await hub.CancelAsync(row.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot cancel purchase", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnDetails(PurchaseRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var window = new PurchaseDetailsWindow(hub.PurchaseService, hub.PaymentService, row.Id);
        window.ShowDialog();
        await hub.LoadAsync();
    }
}