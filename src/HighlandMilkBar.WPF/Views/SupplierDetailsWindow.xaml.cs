using System.Windows;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class SupplierDetailsWindow : Window
{
    private readonly SupplierDetailsViewModel _viewModel;

    public SupplierDetailsWindow(
        ISupplierService suppliers, ISupplierPaymentService payments, IPurchaseService purchases, int supplierId)
    {
        InitializeComponent();
        _viewModel = new SupplierDetailsViewModel(suppliers, payments, purchases);
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync(supplierId);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}