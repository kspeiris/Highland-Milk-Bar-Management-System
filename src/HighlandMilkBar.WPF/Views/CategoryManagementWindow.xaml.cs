using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class CategoryManagementWindow : Window
{
    private readonly CategoryViewModel _viewModel;

    public CategoryManagementWindow(CategoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private async void NewCategory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CategoryEditorWindow { Owner = this };
        if (dialog.ShowDialog() != true || dialog.CreateResult is null) return;

        try
        {
            await _viewModel.CreateCategoryAsync(dialog.CreateResult);
            MessageBox.Show("The category has been created.", "Category saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not create category", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void EditCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedCategory is not { } selected)
        {
            MessageBox.Show("Select a category to edit.", "No category selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new CategoryEditorWindow(selected) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.UpdateResult is null) return;

        try
        {
            await _viewModel.UpdateCategoryAsync(dialog.UpdateResult);
            MessageBox.Show("The category has been updated.", "Category saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not update category", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}