using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class MenuView : UserControl
{
    private MenuViewModel? _vm;

    public MenuView() => InitializeComponent();

    private void MenuView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null || DataContext is not MenuViewModel vm) return;
        _vm = vm;
        vm.RecipeRequested += OpenDetails;
        vm.PricingRequested += OpenDetails;
    }

    private async void OpenDetails(MenuItemDto item)
    {
        if (_vm is null) return;
        if (item.RecipeId is not { } recipeId)
        {
            MessageBox.Show("This menu item has no recipe yet.", "No recipe", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var window = new RecipeDetailsWindow(_vm.RecipeService, _vm.ProductService, recipeId) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        await _vm.SearchAsync();
    }
}