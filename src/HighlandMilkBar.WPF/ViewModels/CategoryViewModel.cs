using System.Collections.ObjectModel;
using System.Windows.Input;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

public sealed class CategoryViewModel : ObservableObject
{
    private readonly ICategoryService _categoryService;

    private string _searchText = string.Empty;
    private StatusFilter? _selectedStatus;
    private CategoryDto? _selectedCategory;
    private bool _isLoading;
    private bool _isBusy;
    private string _statusMessage = "Loading categories…";

    public CategoryViewModel(ICategoryService categoryService)
    {
        _categoryService = categoryService;
        StatusOptions.Add(new StatusFilter("All statuses", null));
        StatusOptions.Add(new StatusFilter("Active", true));
        StatusOptions.Add(new StatusFilter("Inactive", false));

        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        SearchCommand = new RelayCommand(_ => _ = LoadAsync());
        ClearFiltersCommand = new RelayCommand(_ => _ = ClearFiltersAsync());
        SetActiveCommand = new RelayCommand(_ => _ = ToggleActiveAsync(), _ => SelectedCategory is not null);
    }

    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<StatusFilter> StatusOptions { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand SetActiveCommand { get; }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }

    public StatusFilter? SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
                _ = LoadAsync();
        }
    }

    public CategoryDto? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                SetActiveCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var includeInactive = SelectedStatus?.Value ?? true;
            var categories = await _categoryService.GetAllAsync(includeInactive, SearchText);
            Categories.Clear();
            foreach (var c in categories) Categories.Add(c);
            StatusMessage = categories.Count == 0 ? "No categories found." : $"{categories.Count} categories.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load categories: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedStatus = StatusOptions.Count > 0 ? StatusOptions[0] : null;
        await LoadAsync();
    }

    public async Task CreateCategoryAsync(CreateCategoryRequest request)
    {
        await _categoryService.CreateAsync(request);
        await LoadAsync();
        StatusMessage = "Category created.";
    }

    public async Task UpdateCategoryAsync(UpdateCategoryRequest request)
    {
        await _categoryService.UpdateAsync(request);
        await LoadAsync();
        StatusMessage = "Category updated.";
    }

    public async Task ToggleActiveAsync()
    {
        if (SelectedCategory is null) return;
        IsBusy = true;
        try
        {
            var updated = await _categoryService.SetActiveAsync(SelectedCategory.Id, !SelectedCategory.IsActive);
            await LoadAsync();
            StatusMessage = updated.IsActive ? "Category activated." : "Category deactivated.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not update category: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public sealed record StatusFilter(string Label, bool? Value);
}
