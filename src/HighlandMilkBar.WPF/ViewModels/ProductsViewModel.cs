using System.Collections.ObjectModel;
using System.Windows.Input;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.ViewModels;

public sealed class ProductsViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private const int PageSize = 15;

    private string _searchText = string.Empty;
    private CategoryDto? _selectedCategory;
    private TypeFilter? _selectedType;
    private StatusFilter? _selectedStatus;
    private ProductDto? _selectedProduct;
    private int _page = 1;
    private int _totalCount;
    private int _totalPages;
    private bool _isLoading;
    private bool _isBusy;
    private string _statusMessage = "Loading products…";

    private int _totalProducts;
    private int _activeProducts;
    private int _inactiveProducts;
    private int _categoriesCount;

    public ProductsViewModel(IProductService productService, ICategoryService categoryService)
    {
        _productService = productService;
        CategoryService = categoryService;

        TypeOptions.Add(new TypeFilter("All types", null));
        foreach (var type in Enum.GetValues<ProductType>())
            TypeOptions.Add(new TypeFilter(TypeName(type), type));

        StatusOptions.Add(new StatusFilter("All statuses", null));
        StatusOptions.Add(new StatusFilter("Active", true));
        StatusOptions.Add(new StatusFilter("Inactive", false));

        RefreshCommand = new RelayCommand(_ => _ = ReloadAsync());
        SearchCommand = new RelayCommand(_ => _ = SearchAsync());
        ClearFiltersCommand = new RelayCommand(_ => _ = ClearFiltersAsync());
        NextPageCommand = new RelayCommand(_ => _ = GoToPageAsync(Page + 1), _ => HasNextPage);
        PreviousPageCommand = new RelayCommand(_ => _ = GoToPageAsync(Page - 1), _ => HasPreviousPage);
        SetActiveCommand = new RelayCommand(p => _ = ToggleActiveAsync(BindProduct(p)), p => BindProduct(p) is not null);
    }

    public ICategoryService CategoryService { get; }

    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<UnitDto> Units { get; } = new();
    public ObservableCollection<CategoryDto> CategoryFilterOptions { get; } = new();
    public ObservableCollection<TypeFilter> TypeOptions { get; } = new();
    public ObservableCollection<StatusFilter> StatusOptions { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand SetActiveCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public CategoryDto? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public TypeFilter? SelectedType
    {
        get => _selectedType;
        set => SetProperty(ref _selectedType, value);
    }

    public StatusFilter? SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public ProductDto? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                SetActiveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => _selectedProduct is not null;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public int Page { get => _page; private set { if (SetProperty(ref _page, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPagingCommands(); } } }
    public int TotalCount { get => _totalCount; private set { if (SetProperty(ref _totalCount, value)) OnPropertyChanged(nameof(PageLabel)); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPagingCommands(); } } }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
    public string PageLabel => TotalPages == 0 ? "Page 0 of 0" : $"Page {Page} of {TotalPages}  •  {TotalCount} products";

    public int TotalProducts { get => _totalProducts; private set => SetProperty(ref _totalProducts, value); }
    public int ActiveProducts { get => _activeProducts; private set => SetProperty(ref _activeProducts, value); }
    public int InactiveProducts { get => _inactiveProducts; private set => SetProperty(ref _inactiveProducts, value); }
    public int CategoriesCount { get => _categoriesCount; private set => SetProperty(ref _categoriesCount, value); }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        await LoadFilterOptionsAsync();
        await LoadStatsAsync();
        await SearchAsync();
    }

    private async Task LoadFilterOptionsAsync()
    {
        var categories = await _productService.GetCategoriesAsync();
        Categories.Clear();
        foreach (var c in categories) Categories.Add(c);
        Units.Clear();
        foreach (var u in await _productService.GetUnitsAsync()) Units.Add(u);

        CategoryFilterOptions.Clear();
        CategoryFilterOptions.Add(new CategoryDto { Id = 0, Name = "All categories" });
        foreach (var c in categories) CategoryFilterOptions.Add(c);
        if (SelectedCategory is null) SelectedCategory = CategoryFilterOptions[0];
    }

    private async Task LoadStatsAsync()
    {
        var stats = await _productService.GetStatsAsync();
        TotalProducts = stats.TotalProducts;
        ActiveProducts = stats.ActiveProducts;
        InactiveProducts = stats.InactiveProducts;
        CategoriesCount = stats.Categories;
    }

    public async Task ReloadAsync()
    {
        await LoadStatsAsync();
        await SearchAsync();
    }

    public async Task SearchAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            Page = 1;
            var filter = BuildFilter();
            var result = await _productService.SearchAsync(filter);
            Products.Clear();
            foreach (var p in result.Items) Products.Add(p);
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
            Page = result.Page;
            StatusMessage = result.TotalCount == 0
                ? "No products match the current filters."
                : $"{result.TotalCount} product(s) found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load products: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedCategory = CategoryFilterOptions.Count > 0 ? CategoryFilterOptions[0] : null;
        SelectedType = TypeOptions.Count > 0 ? TypeOptions[0] : null;
        SelectedStatus = StatusOptions.Count > 0 ? StatusOptions[0] : null;
        await SearchAsync();
    }

    public async Task GoToPageAsync(int page)
    {
        if (page < 1 || page > TotalPages || page == Page) return;
        var filter = BuildFilter(page);
        IsLoading = true;
        try
        {
            var result = await _productService.SearchAsync(filter);
            Products.Clear();
            foreach (var p in result.Items) Products.Add(p);
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
            Page = result.Page;
            StatusMessage = $"{result.TotalCount} product(s) found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load products: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task CreateProductAsync(CreateProductRequest request)
    {
        await _productService.CreateAsync(request);
        await ReloadAsync();
        StatusMessage = "Product added successfully.";
    }

    public Task<ProductDetailDto?> GetDetailAsync(int id) => _productService.GetByIdAsync(id);

    public async Task UpdateProductAsync(UpdateProductRequest request)
    {
        await _productService.UpdateAsync(request);
        await ReloadAsync();
        StatusMessage = "Product updated successfully.";
    }

    public async Task ToggleActiveAsync(ProductDto? product)
    {
        var target = product ?? SelectedProduct;
        if (target is null) return;
        IsBusy = true;
        try
        {
            var updated = await _productService.SetActiveAsync(target.Id, !target.IsActive);
            await ReloadAsync();
            StatusMessage = updated.IsActive ? "Product activated." : "Product deactivated.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not update product: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ProductFilter BuildFilter(int? page = null) => new()
    {
        Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
        CategoryId = SelectedCategory is { Id: > 0 } c ? c.Id : null,
        Type = SelectedType?.Value,
        IsActive = SelectedStatus?.Value,
        Page = page ?? Page,
        PageSize = PageSize,
        SortBy = "name",
        SortDescending = false
    };

    private void RefreshPagingCommands()
    {
        NextPageCommand.RaiseCanExecuteChanged();
        PreviousPageCommand.RaiseCanExecuteChanged();
    }

    private static ProductDto? BindProduct(object? parameter)
        => parameter as ProductDto;

    private static string TypeName(ProductType type) => type switch
    {
        ProductType.Packaged => "Packaged",
        ProductType.Ingredient => "Ingredient",
        ProductType.PreparedItem => "Prepared item",
        _ => type.ToString()
    };

    public sealed record TypeFilter(string Label, ProductType? Value);
    public sealed record StatusFilter(string Label, bool? Value);
}
