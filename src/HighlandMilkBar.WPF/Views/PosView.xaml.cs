using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class PosView : UserControl
{
    private PosViewModel? _vm;

    public PosView() => InitializeComponent();

    private async void PosView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PosViewModel vm) return;
        if (ReferenceEquals(_vm, vm)) return;
        _vm = vm;
        vm.SaleCompleted += OnSaleCompleted;
        await vm.LoadAsync();
    }

    private void OnSaleCompleted(int saleId)
    {
        var doc = _vm?.LastReceipt;
        if (doc is null) return;

        var window = new ReceiptWindow(doc) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }
}