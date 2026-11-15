using System.Windows;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class RecipeDetailsWindow : Window
{
    private readonly int _recipeId;
    private bool _loaded;

    public RecipeDetailsWindow(IRecipeService recipes, IProductService products, int recipeId)
    {
        InitializeComponent();
        _recipeId = recipeId;
        DataContext = new RecipeDetailsViewModel(recipes, products);
        Loaded += RecipeDetailsWindow_Loaded;
    }

    private async void RecipeDetailsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded || DataContext is not RecipeDetailsViewModel vm) return;
        _loaded = true;
        await vm.LoadAsync(_recipeId);
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RecipeDetailsViewModel vm) return;
        if (vm.Pricing is null)
        {
            MessageBox.Show("Calculate a recommended price first.", "Pricing calculator", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Apply the recommended selling price of LKR {vm.Pricing.RecommendedPriceRounded:0.00}?",
            "Confirm price update", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        await vm.ApplyPriceAsync();
        MessageBox.Show(vm.StatusMessage, "Pricing calculator", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}