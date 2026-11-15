using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class RecipeListView : UserControl
{
    private RecipeListViewModel? _vm;

    public RecipeListView() => InitializeComponent();

    private void RecipeListView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null || DataContext is not RecipeListViewModel vm) return;
        _vm = vm;
        vm.CreateRequested += OpenCreate;
        vm.EditRequested += OpenEdit;
        vm.DetailsRequested += OpenDetails;
    }

    private async void OpenCreate()
    {
        if (_vm is null) return;
        var dialog = new RecipeEditorWindow(_vm.MenuProducts, _vm.Ingredients, editing: null) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.CreateResult is null) return;
        await SaveAsync(() => _vm.CreateNewAsync(dialog.CreateResult!), "Recipe created.");
    }

    private async void OpenEdit(RecipeSummaryDto recipe)
    {
        if (_vm is null) return;
        var detail = await _vm.RecipeService.GetByIdAsync(recipe.Id);
        if (detail is null) return;
        var dialog = new RecipeEditorWindow(_vm.MenuProducts, _vm.Ingredients, detail) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.UpdateResult is null) return;
        await SaveAsync(() => _vm.UpdateExistingAsync(dialog.UpdateResult!), "Recipe updated.");
    }

    private void OpenDetails(RecipeSummaryDto recipe)
    {
        if (_vm is null) return;
        new RecipeDetailsWindow(_vm.RecipeService, _vm.ProductService, recipe.Id) { Owner = Window.GetWindow(this) }.ShowDialog();
        _ = _vm.SearchAsync();
    }

    private async Task SaveAsync(Func<Task<int>> action, string successMessage)
    {
        if (_vm is null) return;
        try
        {
            await action();
            MessageBox.Show(successMessage, "Recipe saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not save recipe", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}