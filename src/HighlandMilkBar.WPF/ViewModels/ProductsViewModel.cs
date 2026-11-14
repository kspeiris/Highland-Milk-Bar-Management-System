using System.Collections.ObjectModel;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

public sealed class ProductsViewModel(IProductService productService) : ObservableObject
{
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string _statusMessage = "Product catalogue";

    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                OnPropertyChanged(nameof(FilteredProducts));
        }
    }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public IEnumerable<ProductDto> FilteredProducts => string.IsNullOrWhiteSpace(SearchText)
        ? Products
        : Products.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || p.Sku.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || p.CategoryName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            var products = await productService.GetAllAsync();
            var categories = await productService.GetCategoriesAsync();
            Products.Clear();
            foreach (var product in products) Products.Add(product);
            Categories.Clear();
            foreach (var category in categories) Categories.Add(category);
            StatusMessage = $"{Products.Count} products in catalogue";
            OnPropertyChanged(nameof(FilteredProducts));
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
        await productService.CreateAsync(request);
        await LoadAsync();
        StatusMessage = "Product added successfully.";
    }
}
