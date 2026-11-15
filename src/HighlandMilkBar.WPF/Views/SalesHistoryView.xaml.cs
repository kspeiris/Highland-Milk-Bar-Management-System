using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class SalesHistoryView : UserControl
{
    private SalesHistoryViewModel? _vm;

    public SalesHistoryView() => InitializeComponent();

    private async void SalesHistoryView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SalesHistoryViewModel vm) return;
        if (ReferenceEquals(_vm, vm)) return;
        _vm = vm;
        vm.DetailsRequested += OnDetails;
        vm.ReceiptRequested += OnReceipt;
        vm.ReturnRequested += OnReturn;
        await vm.LoadAsync();
    }

    private void OnDetails(SaleDetailDto detail)
    {
        var window = new SaleDetailsWindow(detail) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void OnReceipt(ReceiptDocument doc)
    {
        var window = new ReceiptWindow(doc) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void OnReturn(SaleDetailDto detail)
    {
        var window = new SalesReturnWindow(detail, _vm!) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }
}