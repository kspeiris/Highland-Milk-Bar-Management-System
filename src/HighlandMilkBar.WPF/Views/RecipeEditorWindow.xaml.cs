using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class RecipeEditorWindow : Window
{
    public RecipeEditorWindow(IEnumerable<ProductDto> menuProducts, IEnumerable<ProductDto> ingredients, RecipeDetailDto? editing)
    {
        InitializeComponent();
        DataContext = new RecipeEditorViewModel(menuProducts, ingredients, editing);
        if (editing is not null) Title = "Edit recipe";
    }

    public CreateRecipeRequest? CreateResult { get; private set; }
    public UpdateRecipeRequest? UpdateResult { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RecipeEditorViewModel vm) return;

        var error = vm.Validate();
        if (!string.IsNullOrEmpty(error))
        {
            vm.SetError(error);
            return;
        }

        vm.SetError(string.Empty);
        if (vm.IsEditing) UpdateResult = vm.BuildUpdate();
        else CreateResult = vm.BuildCreate();

        DialogResult = true;
    }
}