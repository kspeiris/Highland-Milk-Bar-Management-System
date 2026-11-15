using System.Windows;
using System.Windows.Controls;

namespace HighlandMilkBar.WPF.Views;

public partial class MilkBarView : UserControl
{
    public MilkBarView() => InitializeComponent();

    private async void MilkBarView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MilkBarViewModel vm)
            await vm.LoadAsync();
    }
}