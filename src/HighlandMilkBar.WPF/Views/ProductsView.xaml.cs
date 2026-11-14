using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.Application.DTOs;
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
        var dialog = new ProductEditorWindow(viewModel.Categories, viewModel.Units) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.CreateResult is null) return;

        try
        {
            await viewModel.CreateProductAsync(dialog.CreateResult);
            MessageBox.Show("The product has been added to the catalogue.", "Product saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not save product", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductsViewModel viewModel) return;
        if (viewModel.SelectedProduct is not { } selected)
        {
            MessageBox.Show("Select a product to edit.", "No product selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var detail = await viewModel.GetDetailAsync(selected.Id);
        if (detail is null) return;

        var dialog = new ProductEditorWindow(viewModel.Categories, viewModel.Units, detail) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.UpdateResult is null) return;

        try
        {
            await viewModel.UpdateProductAsync(dialog.UpdateResult);
            MessageBox.Show("The product has been updated.", "Product saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not update product", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void DetailsProduct_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductsViewModel viewModel) return;
        if (viewModel.SelectedProduct is not { } selected)
        {
            MessageBox.Show("Select a product to view its details.", "No product selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var detail = await viewModel.GetDetailAsync(selected.Id);
            if (detail is null) return;
            new ProductDetailsWindow(detail) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not load product", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Categories_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProductsViewModel viewModel) return;
        var window = new CategoryManagementWindow(new CategoryViewModel(viewModel.CategoryService)) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        _ = viewModel.ReloadAsync();
    }
}