using System.Collections.ObjectModel;
using System.Globalization;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Ingredient list: inventory-tracked products used in recipes, with sellable stock.</summary>
public sealed class IngredientListViewModel : ObservableObject
{
    private readonly IMilkBarService _milkBar;
    private string _searchText = string.Empty;
    private StockStatus? _stockFilter;

    public IngredientListViewModel(IMilkBarService milkBar)
    {
        _milkBar = milkBar;
        SearchCommand = new RelayCommand(_ => _ = SearchAsync());
    }

    public ObservableCollection<IngredientDto> Ingredients { get; } = new();
    public Array StockFilters => Enum.GetValues<StockStatus>();
    public RelayCommand SearchCommand { get; }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public StockStatus? StockFilter { get => _stockFilter; set { if (SetProperty(ref _stockFilter, value)) _ = SearchAsync(); } }

    public async Task LoadAsync() => await SearchAsync();

    public async Task SearchAsync()
    {
        var result = await _milkBar.GetIngredientsAsync(new IngredientFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            Stock = StockFilter,
            PageSize = 200
        });
        Ingredients.Clear();
        foreach (var i in result.Items) Ingredients.Add(i);
        OnPropertyChanged(nameof(ResultCount));
    }

    public int ResultCount => Ingredients.Count;
}

/// <summary>A single ingredient row inside the recipe editor.</summary>
public sealed class RecipeEditorLine : ObservableObject
{
    private string _quantityText = string.Empty;
    public Action? Changed { get; set; }

    public int IngredientProductId { get; init; }
    public string IngredientName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal UnitCost { get; init; }
    public decimal AvailableStock { get; init; }

    public string QuantityText
    {
        get => _quantityText;
        set
        {
            if (SetProperty(ref _quantityText, value))
            {
                OnPropertyChanged(nameof(LineCost));
                Changed?.Invoke();
            }
        }
    }

    public decimal Quantity => Parse(_quantityText);
    public decimal LineCost => Quantity * UnitCost;
    public string AvailabilityText => $"Stock {AvailableStock:0.###} {Unit}";

    private static decimal Parse(string? text)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var v) ? v
            : decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var v2) ? v2 : 0m;
}

/// <summary>Editor for creating or editing a recipe (general info + dynamic ingredient rows).</summary>
public sealed class RecipeEditorViewModel : ObservableObject
{
    private string _name = string.Empty;
    private ProductDto? _selectedMenu;
    private string _yieldText = "1";
    private string _yieldUnit = "Portion";
    private bool _isActive = true;
    private ProductDto? _selectedIngredient;
    private string _errorMessage = string.Empty;

    public RecipeEditorViewModel(IEnumerable<ProductDto> menuProducts, IEnumerable<ProductDto> ingredients, RecipeDetailDto? editing)
    {
        MenuProducts = new ObservableCollection<ProductDto>(menuProducts);
        Ingredients = new ObservableCollection<ProductDto>(ingredients);
        YieldUnits = new[] { "Portion", "Glass", "Cup", "Serving", "Batch" };

        AddIngredientCommand = new RelayCommand(_ => AddIngredient(), _ => SelectedIngredient is not null);
        RemoveLineCommand = new RelayCommand(p => RemoveLine(p as RecipeEditorLine));

        if (editing is not null)
        {
            EditingId = editing.Id;
            Name = editing.Name;
            YieldText = editing.YieldQuantity.ToString("0.###", CultureInfo.CurrentCulture);
            YieldUnit = editing.YieldUnit;
            IsActive = editing.IsActive;
            SelectedMenu = MenuProducts.FirstOrDefault(p => p.Id == editing.MenuProductId);
            foreach (var item in editing.Items)
            {
                var line = new RecipeEditorLine
                {
                    IngredientProductId = item.IngredientProductId,
                    IngredientName = item.IngredientName,
                    Unit = item.StockUnit,
                    UnitCost = item.UnitCost,
                    AvailableStock = item.AvailableStock
                };
                line.QuantityText = item.QuantityRequired.ToString("0.###", CultureInfo.CurrentCulture);
                line.Changed = RefreshTotals;
                Lines.Add(line);
            }
        }
    }

    public ObservableCollection<ProductDto> MenuProducts { get; }
    public ObservableCollection<ProductDto> Ingredients { get; }
    public ObservableCollection<RecipeEditorLine> Lines { get; } = new();
    public IReadOnlyList<string> YieldUnits { get; }

    public RelayCommand AddIngredientCommand { get; }
    public RelayCommand RemoveLineCommand { get; }

    public bool IsEditing => EditingId > 0;
    public int EditingId { get; private set; }

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public ProductDto? SelectedMenu { get => _selectedMenu; set => SetProperty(ref _selectedMenu, value); }
    public string YieldText { get => _yieldText; set { if (SetProperty(ref _yieldText, value)) { OnPropertyChanged(nameof(CostPerServing)); OnPropertyChanged(nameof(YieldValue)); } } }
    public string YieldUnit { get => _yieldUnit; set => SetProperty(ref _yieldUnit, value); }
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public ProductDto? SelectedIngredient { get => _selectedIngredient; set { if (SetProperty(ref _selectedIngredient, value)) AddIngredientCommand.RaiseCanExecuteChanged(); } }

    public decimal YieldValue => decimal.TryParse(YieldText, NumberStyles.Number, CultureInfo.CurrentCulture, out var v) ? v
        : decimal.TryParse(YieldText, NumberStyles.Number, CultureInfo.InvariantCulture, out var v2) ? v2 : 0m;
    public decimal TotalCost => Lines.Sum(l => l.LineCost);
    public decimal CostPerServing => YieldValue > 0 ? TotalCost / YieldValue : 0m;
    public int LineCount => Lines.Count;
    public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public void SetError(string? message) => ErrorMessage = message ?? string.Empty;

    private void AddIngredient()
    {
        var ingredient = SelectedIngredient;
        if (ingredient is null) return;
        if (Lines.Any(l => l.IngredientProductId == ingredient.Id))
        {
            ErrorMessage = $"'{ingredient.Name}' is already in the recipe.";
            return;
        }

        var line = new RecipeEditorLine
        {
            IngredientProductId = ingredient.Id,
            IngredientName = ingredient.Name,
            Unit = ingredient.Unit,
            UnitCost = ingredient.CostPrice,
            AvailableStock = ingredient.CurrentStock
        };
        line.QuantityText = "1";
        line.Changed = RefreshTotals;
        Lines.Add(line);
        ErrorMessage = string.Empty;
        SelectedIngredient = null;
    }

    private void RemoveLine(RecipeEditorLine? line)
    {
        if (line is null) return;
        Lines.Remove(line);
        RefreshTotals();
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(TotalCost));
        OnPropertyChanged(nameof(CostPerServing));
        OnPropertyChanged(nameof(LineCount));
    }

    public string Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return "A recipe name is required.";
        if (SelectedMenu is null) return "Select a menu product.";
        if (YieldValue <= 0) return "Recipe yield must be greater than zero.";
        if (Lines.Count == 0) return "Add at least one ingredient.";
        if (Lines.Any(l => l.Quantity <= 0)) return "Every ingredient quantity must be greater than zero.";
        return string.Empty;
    }

    public CreateRecipeRequest BuildCreate()
        => new()
        {
            Name = Name.Trim(),
            MenuProductId = SelectedMenu!.Id,
            YieldQuantity = YieldValue,
            YieldUnit = YieldUnit,
            IsActive = IsActive,
            Items = Lines.Select(l => new RecipeLineRequest { IngredientProductId = l.IngredientProductId, Quantity = l.Quantity, Unit = l.Unit }).ToList()
        };

    public UpdateRecipeRequest BuildUpdate()
        => new()
        {
            Id = EditingId,
            Name = Name.Trim(),
            MenuProductId = SelectedMenu!.Id,
            YieldQuantity = YieldValue,
            YieldUnit = YieldUnit,
            IsActive = IsActive,
            Items = Lines.Select(l => new RecipeLineRequest { IngredientProductId = l.IngredientProductId, Quantity = l.Quantity, Unit = l.Unit }).ToList()
        };
}

/// <summary>Recipe details, cost breakdown, ingredient availability and pricing calculator.</summary>
public sealed class RecipeDetailsViewModel : ObservableObject
{
    private readonly IRecipeService _recipes;
    private readonly IProductService _products;
    private RecipeDetailDto? _detail;
    private string _targetMarginText = "40";
    private string _roundingText = "0";
    private PricingRecommendationDto? _pricing;
    private string _statusMessage = string.Empty;
    private bool _isError;

    public RecipeDetailsViewModel(IRecipeService recipes, IProductService products)
    {
        _recipes = recipes;
        _products = products;
        CalculatePricingCommand = new RelayCommand(_ => _ = RecalculatePricingAsync());
        ApplyPriceCommand = new RelayCommand(_ => _ = ApplyPriceAsync(), _ => Pricing is { MeetsTarget: false } && Pricing.RecommendedPriceRounded > 0);
    }

    public ObservableCollection<RecipeDetailItemDto> Items { get; } = new();
    public ObservableCollection<IngredientAvailabilityLine> Availability { get; } = new();
    public ObservableCollection<RecipeCostLine> CostLines { get; } = new();

    public RelayCommand CalculatePricingCommand { get; }
    public RelayCommand ApplyPriceCommand { get; }

    public event Action<int>? PriceApplied;

    public string Name { get; private set; } = string.Empty;
    public string MenuProductName { get; private set; } = string.Empty;
    public string CategoryName { get; private set; } = string.Empty;
    public string YieldText { get; private set; } = string.Empty;
    public string StatusText { get; private set; } = string.Empty;
    public decimal SellingPrice { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal CostPerServing { get; private set; }
    public decimal GrossProfit { get; private set; }
    public decimal MarginPercent { get; private set; }
    public string MaxServingsText { get; private set; } = string.Empty;
    public bool AllAvailable { get; private set; }

    public PricingRecommendationDto? Pricing { get => _pricing; private set { if (SetProperty(ref _pricing, value)) ApplyPriceCommand.RaiseCanExecuteChanged(); } }
    public string TargetMarginText { get => _targetMarginText; set => SetProperty(ref _targetMarginText, value); }
    public string RoundingText { get => _roundingText; set => SetProperty(ref _roundingText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool IsError { get => _isError; private set => SetProperty(ref _isError, value); }

    public async Task LoadAsync(int recipeId)
    {
        var detail = await _recipes.GetByIdAsync(recipeId);
        if (detail is null) return;
        _detail = detail;

        Name = detail.Name;
        MenuProductName = detail.MenuProductName;
        CategoryName = detail.CategoryName;
        YieldText = $"{detail.YieldQuantity:0.###} {detail.YieldUnit}";
        StatusText = detail.IsActive ? "Active" : "Inactive";
        SellingPrice = detail.SellingPrice;
        TotalCost = detail.Cost.TotalCost;
        CostPerServing = detail.Cost.CostPerServing;
        GrossProfit = detail.Cost.GrossProfit;
        MarginPercent = detail.Cost.MarginPercent;
        AllAvailable = detail.Availability.AllAvailable;
        MaxServingsText = detail.Availability.IsUnlimited ? "Unlimited" : detail.Availability.MaxServings.ToString();

        Items.Clear();
        foreach (var i in detail.Items) Items.Add(i);
        CostLines.Clear();
        foreach (var l in detail.Cost.Lines) CostLines.Add(l);
        Availability.Clear();
        foreach (var a in detail.Availability.Lines) Availability.Add(a);

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(MenuProductName));
        OnPropertyChanged(nameof(CategoryName));
        OnPropertyChanged(nameof(YieldText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SellingPrice));
        OnPropertyChanged(nameof(TotalCost));
        OnPropertyChanged(nameof(CostPerServing));
        OnPropertyChanged(nameof(GrossProfit));
        OnPropertyChanged(nameof(MarginPercent));
        OnPropertyChanged(nameof(MaxServingsText));
        OnPropertyChanged(nameof(AllAvailable));

        await RecalculatePricingAsync();
    }

    public async Task RecalculatePricingAsync()
    {
        if (_detail is null) return;
        if (!ParseDecimal(TargetMarginText, out var target)) { ShowError("Enter a valid target margin (0-99%)."); return; }
        ParseDecimal(RoundingText, out var rounding);
        try
        {
            Pricing = await _recipes.CalculatePricingAsync(_detail.Id, target, rounding);
            ClearError();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    public async Task ApplyPriceAsync()
    {
        if (_detail is null || Pricing is null) return;
        var product = await _products.GetByIdAsync(_detail.MenuProductId);
        if (product is null) { ShowError("The menu product could not be found."); return; }

        var request = new UpdateProductRequest
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            Barcode = product.Barcode,
            CategoryId = product.CategoryId,
            Type = product.Type,
            Unit = product.Unit,
            CostPrice = product.CostPrice,
            SellingPrice = Pricing.RecommendedPriceRounded,
            ReorderLevel = product.ReorderLevel,
            TrackInventory = product.TrackInventory,
            RequiresExpiryDate = product.RequiresExpiryDate,
            IsActive = product.IsActive
        };
        await _products.UpdateAsync(request);
        StatusMessage = $"Selling price updated to LKR {Pricing.RecommendedPriceRounded:0.00}.";
        IsError = false;
        PriceApplied?.Invoke(product.Id);
    }

    private static bool ParseDecimal(string? text, out decimal value)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private void ShowError(string message) { IsError = true; StatusMessage = message; }
    private void ClearError() { IsError = false; StatusMessage = string.Empty; }
}