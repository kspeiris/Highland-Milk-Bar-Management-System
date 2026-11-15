using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class InventoryView : UserControl
{
    public InventoryView() => InitializeComponent();

    private async void InventoryView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel vm)
            await vm.LoadAsync();
    }
}