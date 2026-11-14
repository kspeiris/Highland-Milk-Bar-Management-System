using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class ProductsView : UserControl
{
    private bool _loaded;

    public ProductsView()
    {
        InitializeComponent();
    }

    private async void ProductsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded || DataContext is not ProductsViewModel viewModel) return;
        _loaded = true;
        await viewModel.LoadAsync();
    }

    private async void AddProduct_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductsViewModel viewModel) return;
        var dialog = new ProductEditorWindow(viewModel.Categories) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        try
        {
            await viewModel.CreateProductAsync(dialog.Result);
            MessageBox.Show("The product has been added to the catalogue.", "Product saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not save product", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
