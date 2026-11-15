using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Hub view model for the Milk Bar workspace. Hosts one child VM per section.</summary>
public sealed class MilkBarViewModel : ObservableObject
{
    private bool _isLoading;

    public MilkBarViewModel(IMilkBarService milkBar, IRecipeService recipes, IProductService products)
    {
        Overview = new MilkBarOverviewViewModel(milkBar);
        Menu = new MenuViewModel(milkBar, recipes, products);
        Recipes = new RecipeListViewModel(recipes, products);
        Ingredients = new IngredientListViewModel(milkBar);
        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
    }

    public MilkBarOverviewViewModel Overview { get; }
    public MenuViewModel Menu { get; }
    public RecipeListViewModel Recipes { get; }
    public IngredientListViewModel Ingredients { get; }

    public RelayCommand RefreshCommand { get; }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            await Overview.LoadAsync();
            await Menu.LoadAsync();
            await Recipes.LoadAsync();
            await Ingredients.LoadAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>KPIs, charts and alerts for the Milk Bar overview.</summary>
public sealed class MilkBarOverviewViewModel : ObservableObject
{
    private readonly IMilkBarService _milkBar;
    private int _totalMenuItems;
    private int _activeRecipes;
    private int _totalIngredients;
    private int _lowStockIngredients;
    private decimal _averageCost;
    private decimal _averageMargin;

    public MilkBarOverviewViewModel(IMilkBarService milkBar) => _milkBar = milkBar;

    public ObservableCollection<CategoryCountDto> MenuByCategory { get; } = new();
    public ObservableCollection<RecipeCostRankDto> HighestCostRecipes { get; } = new();
    public ObservableCollection<MenuMarginRankDto> HighestMarginProducts { get; } = new();
    public ObservableCollection<MenuMarginRankDto> LowestMarginProducts { get; } = new();
    public ObservableCollection<IngredientCostShareDto> IngredientCostBreakdown { get; } = new();
    public ObservableCollection<MilkBarAlertDto> Alerts { get; } = new();

    public int TotalMenuItems { get => _totalMenuItems; private set => SetProperty(ref _totalMenuItems, value); }
    public int ActiveRecipes { get => _activeRecipes; private set => SetProperty(ref _activeRecipes, value); }
    public int TotalIngredients { get => _totalIngredients; private set => SetProperty(ref _totalIngredients, value); }
    public int LowStockIngredients { get => _lowStockIngredients; private set => SetProperty(ref _lowStockIngredients, value); }
    public decimal AverageCost { get => _averageCost; private set => SetProperty(ref _averageCost, value); }
    public decimal AverageMargin { get => _averageMargin; private set => SetProperty(ref _averageMargin, value); }
    public bool HasAlerts => Alerts.Count > 0;

    public async Task LoadAsync()
    {
        var data = await _milkBar.GetOverviewAsync();
        TotalMenuItems = data.TotalMenuItems;
        ActiveRecipes = data.ActiveRecipes;
        TotalIngredients = data.TotalIngredientsUsed;
        LowStockIngredients = data.LowStockIngredients;
        AverageCost = data.AverageRecipeCost;
        AverageMargin = data.AverageMarginPercent;

        MenuByCategory.Clear();
        foreach (var c in data.MenuByCategory) MenuByCategory.Add(c);
        HighestCostRecipes.Clear();
        foreach (var r in data.HighestCostRecipes) HighestCostRecipes.Add(r);
        HighestMarginProducts.Clear();
        foreach (var m in data.HighestMarginProducts) HighestMarginProducts.Add(m);
        LowestMarginProducts.Clear();
        foreach (var m in data.LowestMarginProducts) LowestMarginProducts.Add(m);
        IngredientCostBreakdown.Clear();
        foreach (var i in data.IngredientCostBreakdown) IngredientCostBreakdown.Add(i);
        Alerts.Clear();
        foreach (var a in data.Alerts) Alerts.Add(a);
        OnPropertyChanged(nameof(HasAlerts));
    }
}

/// <summary>Menu management: prepared products with recipe cost and margin.</summary>
public sealed class MenuViewModel : ObservableObject
{
    private readonly IMilkBarService _milkBar;
    private readonly IRecipeService _recipes;
    private readonly IProductService _products;
    private string _searchText = string.Empty;
    private CategoryDto? _selectedCategory;
    private MenuItemDto? _selectedItem;

    public MenuViewModel(IMilkBarService milkBar, IRecipeService recipes, IProductService products)
    {
        _milkBar = milkBar;
        _recipes = recipes;
        _products = products;
        LoadCategoriesCommand = new RelayCommand(_ => _ = LoadCategoriesAsync());
        SearchCommand = new RelayCommand(_ => _ = SearchAsync());
        ViewRecipeCommand = new RelayCommand(_ => OpenRecipe(), _ => SelectedItem is { HasActiveRecipe: true });
        ViewPricingCommand = new RelayCommand(_ => OpenPricing(), _ => SelectedItem is { HasActiveRecipe: true });
    }

    public ObservableCollection<MenuItemDto> Items { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();

    public RelayCommand LoadCategoriesCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ViewRecipeCommand { get; }
    public RelayCommand ViewPricingCommand { get; }

    public event Action<MenuItemDto>? RecipeRequested;
    public event Action<MenuItemDto>? PricingRequested;

    public IRecipeService RecipeService => _recipes;
    public IProductService ProductService => _products;

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public CategoryDto? SelectedCategory { get => _selectedCategory; set { if (SetProperty(ref _selectedCategory, value)) _ = SearchAsync(); } }
    public MenuItemDto? SelectedItem { get => _selectedItem; set { if (SetProperty(ref _selectedItem, value)) { ViewRecipeCommand.RaiseCanExecuteChanged(); ViewPricingCommand.RaiseCanExecuteChanged(); } } }

    public async Task LoadAsync()
    {
        await LoadCategoriesAsync();
        await SearchAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _products.GetCategoriesAsync();
        Categories.Clear();
        Categories.Add(new CategoryDto { Id = 0, Name = "All categories" });
        foreach (var c in categories) Categories.Add(c);
        SelectedCategory = Categories[0];
    }

    public async Task SearchAsync()
    {
        var result = await _milkBar.GetMenuItemsAsync(new MenuFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            CategoryId = SelectedCategory is { Id: > 0 } c ? c.Id : null,
            PageSize = 200
        });
        Items.Clear();
        foreach (var i in result.Items) Items.Add(i);
        OnPropertyChanged(nameof(ResultCount));
    }

    public int ResultCount => Items.Count;

    private void OpenRecipe() { if (SelectedItem is { } i) RecipeRequested?.Invoke(i); }
    private void OpenPricing() { if (SelectedItem is { } i) PricingRequested?.Invoke(i); }
}

/// <summary>Recipe list with create/edit/activate actions.</summary>
public sealed class RecipeListViewModel : ObservableObject
{
    private readonly IRecipeService _recipes;
    private readonly IProductService _products;
    private string _searchText = string.Empty;
    private CategoryDto? _selectedCategory;
    private bool _activeOnly;
    private RecipeSummaryDto? _selectedRecipe;

    public RecipeListViewModel(IRecipeService recipes, IProductService products)
    {
        _recipes = recipes;
        _products = products;
        LoadDropDownsCommand = new RelayCommand(_ => _ = LoadDropDownsAsync());
        SearchCommand = new RelayCommand(_ => _ = SearchAsync());
        CreateCommand = new RelayCommand(_ => CreateRequested?.Invoke());
        EditCommand = new RelayCommand(_ => Edit(), _ => SelectedRecipe is not null);
        DetailsCommand = new RelayCommand(_ => Details(), _ => SelectedRecipe is not null);
        ToggleActiveCommand = new RelayCommand(_ => _ = ToggleActiveAsync(), _ => SelectedRecipe is not null);
    }

    public ObservableCollection<RecipeSummaryDto> Recipes { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<ProductDto> MenuProducts { get; } = new();
    public ObservableCollection<ProductDto> Ingredients { get; } = new();

    public RelayCommand LoadDropDownsCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand CreateCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DetailsCommand { get; }
    public RelayCommand ToggleActiveCommand { get; }

    public event Action? CreateRequested;
    public event Action<RecipeSummaryDto>? EditRequested;
    public event Action<RecipeSummaryDto>? DetailsRequested;

    public IRecipeService RecipeService => _recipes;
    public IProductService ProductService => _products;

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public bool ActiveOnly { get => _activeOnly; set { if (SetProperty(ref _activeOnly, value)) _ = SearchAsync(); } }
    public CategoryDto? SelectedCategory { get => _selectedCategory; set { if (SetProperty(ref _selectedCategory, value)) _ = SearchAsync(); } }
    public RecipeSummaryDto? SelectedRecipe
    {
        get => _selectedRecipe;
        set
        {
            if (SetProperty(ref _selectedRecipe, value))
            {
                EditCommand.RaiseCanExecuteChanged();
                DetailsCommand.RaiseCanExecuteChanged();
                ToggleActiveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task LoadAsync()
    {
        await LoadDropDownsAsync();
        await SearchAsync();
    }

    private async Task LoadDropDownsAsync()
    {
        var categories = await _products.GetCategoriesAsync();
        Categories.Clear();
        Categories.Add(new CategoryDto { Id = 0, Name = "All categories" });
        foreach (var c in categories) Categories.Add(c);
        SelectedCategory = Categories[0];

        MenuProducts.Clear();
        foreach (var p in await ProductLoader.LoadAsync(_products, trackedOnly: false))
            if (p.Type == ProductType.PreparedItem) MenuProducts.Add(p);

        Ingredients.Clear();
        foreach (var p in await ProductLoader.LoadAsync(_products, trackedOnly: true))
            Ingredients.Add(p);
    }

    public async Task SearchAsync()
    {
        var result = await _recipes.SearchAsync(new RecipeFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            MenuCategoryId = SelectedCategory is { Id: > 0 } c ? c.Id : null,
            IsActive = ActiveOnly ? true : null,
            PageSize = 200
        });
        Recipes.Clear();
        foreach (var r in result.Items) Recipes.Add(r);
        OnPropertyChanged(nameof(ResultCount));
    }

    public int ResultCount => Recipes.Count;

    private void Edit() { if (SelectedRecipe is { } r) EditRequested?.Invoke(r); }
    private void Details() { if (SelectedRecipe is { } r) DetailsRequested?.Invoke(r); }

    private async Task ToggleActiveAsync()
    {
        if (SelectedRecipe is not { } r) return;
        await _recipes.SetActiveAsync(r.Id, !r.IsActive);
        await SearchAsync();
    }

    public Task<int> CreateNewAsync(CreateRecipeRequest request) => SaveAndReloadAsync(() => _recipes.CreateAsync(request));
    public Task<int> UpdateExistingAsync(UpdateRecipeRequest request) => SaveAndReloadAsync(() => _recipes.UpdateAsync(request));

    private async Task<int> SaveAndReloadAsync(Func<Task<RecipeSummaryDto>> action)
    {
        var saved = await action();
        await SearchAsync();
        return saved.Id;
    }
}