using System.Windows;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class PurchaseDetailsWindow : Window
{
    private readonly PurchaseDetailsViewModel _viewModel;
    private readonly int _purchaseId;

    public PurchaseDetailsWindow(IPurchaseService purchases, ISupplierPaymentService payments, int purchaseId)
    {
        InitializeComponent();
        _purchaseId = purchaseId;
        _viewModel = new PurchaseDetailsViewModel(purchases, payments);
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync(_purchaseId);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}