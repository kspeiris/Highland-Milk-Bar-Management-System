using System.Collections.ObjectModel;
using System.Windows.Input;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Pages through the product catalogue for dropdowns (the search API caps page size at 200).</summary>
internal static class ProductLoader
{
    public static async Task<List<ProductDto>> LoadAsync(IProductService products, bool trackedOnly)
    {
        var all = new List<ProductDto>();
        var page = 1;
        while (true)
        {
            var result = await products.SearchAsync(new ProductFilter { IsActive = true, Page = page, PageSize = 200 });
            all.AddRange(result.Items);
            if (result.TotalPages == 0 || page >= result.TotalPages || result.Items.Count == 0) break;
            page++;
        }

        return trackedOnly ? all.Where(p => p.TrackInventory).ToList() : all;
    }
}

/// <summary>Hub view model for the Inventory workspace. Hosts one child VM per tab.</summary>
public sealed class InventoryViewModel : ObservableObject
{
    private bool _isLoading;

    public InventoryViewModel(IInventoryQueryService queries, IInventoryService inventory, IProductService products)
    {
        Overview = new InventoryOverviewViewModel(queries);
        Stock = new StockOverviewViewModel(queries, products);
        Batches = new BatchViewModel(queries, products);
        StockIn = new StockInViewModel(inventory, products);
        Adjustments = new StockAdjustmentViewModel(inventory, products);
        Movements = new StockMovementViewModel(queries, products);
        Expiry = new ExpiryViewModel(queries);

        StockIn.Saved = ReloadDerivedAsync;
        Adjustments.Saved = ReloadDerivedAsync;
        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
    }

    public RelayCommand RefreshCommand { get; }

    public InventoryOverviewViewModel Overview { get; }
    public StockOverviewViewModel Stock { get; }
    public BatchViewModel Batches { get; }
    public StockInViewModel StockIn { get; }
    public StockAdjustmentViewModel Adjustments { get; }
    public StockMovementViewModel Movements { get; }
    public ExpiryViewModel Expiry { get; }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            await Overview.LoadAsync();
            await Stock.LoadAsync();
            await Batches.LoadAsync();
            await StockIn.LoadProductsAsync();
            await Adjustments.LoadProductsAsync();
            await Movements.LoadAsync();
            await Expiry.LoadAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Refreshes the read-only tabs after a stock mutation (receipt or adjustment).</summary>
    private async Task ReloadDerivedAsync()
    {
        await Overview.LoadAsync();
        await Stock.LoadAsync();
        await Batches.LoadAsync();
        await Expiry.LoadAsync();
        await Movements.LoadAsync();
    }
}

/// <summary>KPIs, category breakdowns and alerts for the inventory overview tab.</summary>
public sealed class InventoryOverviewViewModel : ObservableObject
{
    private readonly IInventoryQueryService _queries;
    private InventoryOverviewDto? _overview;
    private bool _isLoading;
    private string _statusMessage = "Loading overview…";

    public InventoryOverviewViewModel(IInventoryQueryService queries) => _queries = queries;

    public InventoryOverviewDto? Overview
    {
        get => _overview;
        private set
        {
            if (SetProperty(ref _overview, value))
            {
                OnPropertyChanged(nameof(HasData));
                OnPropertyChanged(nameof(AlertCount));
            }
        }
    }

    public bool HasData => _overview is not null;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public int AlertCount => (Overview?.LowStockAlerts.Count ?? 0) + (Overview?.ExpiryAlerts.Count ?? 0);

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Overview = await _queries.GetOverviewAsync();
            StatusMessage = Overview is null
                ? "No inventory data."
                : $"{Overview.TrackedProducts} tracked product(s) • {Overview.TotalActiveBatches} active batch(es).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load overview: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

/// <summary>Stock overview grid with search, category and status filters.</summary>
public sealed class StockOverviewViewModel : ObservableObject
{
    private readonly IInventoryQueryService _queries;
    private readonly IProductService _products;
    private const int PageSize = 20;

    private string _searchText = string.Empty;
    private CategoryDto? _selectedCategory;
    private StockStatusFilter? _selectedStatus;
    private StockRowDto? _selectedRow;
    private int _page = 1;
    private int _totalCount;
    private int _totalPages;
    private bool _isLoading;
    private string _statusMessage = "Loading stock…";

    public StockOverviewViewModel(IInventoryQueryService queries, IProductService products)
    {
        _queries = queries;
        _products = products;

        StatusOptions.Add(new StockStatusFilter("All statuses", null));
        StatusOptions.Add(new StockStatusFilter("In stock", StockStatus.InStock));
        StatusOptions.Add(new StockStatusFilter("Low stock", StockStatus.LowStock));
        StatusOptions.Add(new StockStatusFilter("Out of stock", StockStatus.OutOfStock));

        RefreshCommand = new RelayCommand(_ => _ = ReloadAsync());
        SearchCommand = new RelayCommand(_ => _ = SearchAsync());
        ClearFiltersCommand = new RelayCommand(_ => _ = ClearFiltersAsync());
        NextPageCommand = new RelayCommand(_ => _ = GoToPageAsync(Page + 1), _ => HasNextPage);
        PreviousPageCommand = new RelayCommand(_ => _ = GoToPageAsync(Page - 1), _ => HasPreviousPage);
    }

    public ObservableCollection<StockRowDto> Rows { get; } = new();
    public ObservableCollection<CategoryDto> CategoryOptions { get; } = new();
    public ObservableCollection<StockStatusFilter> StatusOptions { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand PreviousPageCommand { get; }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public CategoryDto? SelectedCategory { get => _selectedCategory; set => SetProperty(ref _selectedCategory, value); }
    public StockStatusFilter? SelectedStatus { get => _selectedStatus; set => SetProperty(ref _selectedStatus, value); }
    public StockRowDto? SelectedRow { get => _selectedRow; set => SetProperty(ref _selectedRow, value); }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public int Page
    {
        get => _page;
        private set { if (SetProperty(ref _page, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPagingCommands(); } }
    }
    public int TotalCount { get => _totalCount; private set { if (SetProperty(ref _totalCount, value)) OnPropertyChanged(nameof(PageLabel)); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPagingCommands(); } } }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
    public string PageLabel => TotalPages == 0 ? "Page 0 of 0" : $"Page {Page} of {TotalPages}  •  {TotalCount} product(s)";

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        await LoadFilterOptionsAsync();
        await SearchAsync();
    }

    private async Task LoadFilterOptionsAsync()
    {
        var categories = await _products.GetCategoriesAsync();
        CategoryOptions.Clear();
        CategoryOptions.Add(new CategoryDto { Id = 0, Name = "All categories" });
        foreach (var c in categories) CategoryOptions.Add(c);
        if (SelectedCategory is null) SelectedCategory = CategoryOptions[0];
        if (SelectedStatus is null) SelectedStatus = StatusOptions[0];
    }

    public async Task ReloadAsync()
    {
        await SearchAsync();
    }

    public async Task SearchAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            await RunQueryAsync(1);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load stock: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GoToPageAsync(int page)
    {
        if (page < 1 || page > TotalPages || page == Page) return;
        IsLoading = true;
        try
        {
            await RunQueryAsync(page);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load stock: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RunQueryAsync(int page)
    {
        var filter = new StockFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            CategoryId = SelectedCategory is { Id: > 0 } c ? c.Id : null,
            Status = SelectedStatus?.Value,
            Page = page,
            PageSize = PageSize
        };

        var result = await _queries.GetStockAsync(filter);
        Rows.Clear();
        foreach (var row in result.Items) Rows.Add(row);
        TotalCount = result.TotalCount;
        TotalPages = result.TotalPages;
        Page = result.Page;
        StatusMessage = result.TotalCount == 0
            ? "No stock rows match the current filters."
            : $"{result.TotalCount} product(s) found.";
    }

    public async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedCategory = CategoryOptions.Count > 0 ? CategoryOptions[0] : null;
        SelectedStatus = StatusOptions.Count > 0 ? StatusOptions[0] : null;
        await SearchAsync();
    }

    private void RefreshPagingCommands()
    {
        NextPageCommand.RaiseCanExecuteChanged();
        PreviousPageCommand.RaiseCanExecuteChanged();
    }

    public sealed record StockStatusFilter(string Label, StockStatus? Value);
}

/// <summary>Batch management grid with search, product and expiry filters.</summary>
public sealed class BatchViewModel : ObservableObject
{
    private readonly IInventoryQueryService _queries;
    private readonly IProductService _products;
    private const int PageSize = 20;

    private string _searchText = string.Empty;
    private ProductDto? _selectedProduct;
    private ExpiryFilterOption? _selectedExpiry;
    private BatchRowDto? _selectedBatch;
    private int _page = 1;
    private int _totalCount;
    private int _totalPages;
    private bool _isLoading;
    private string _statusMessage = "Loading batches…";

    public BatchViewModel(IInventoryQueryService queries, IProductService products)
    {
        _queries = queries;
        _products = products;

        ExpiryOptions.Add(new ExpiryFilterOption("All batches", ExpiryFilter.All));
        ExpiryOptions.Add(new ExpiryFilterOption("Near expiry", ExpiryFilter.NearExpiry));
        ExpiryOptions.Add(new ExpiryFilterOption("Expired", ExpiryFilter.Expired));

        RefreshCommand = new RelayCommand(_ => _ = ReloadAsync());
        SearchCommand = new RelayCommand(_ => _ = SearchAsync());
        ClearFiltersCommand = new RelayCommand(_ => _ = ClearFiltersAsync());
        NextPageCommand = new RelayCommand(_ => _ = GoToPageAsync(Page + 1), _ => HasNextPage);
        PreviousPageCommand = new RelayCommand(_ => _ = GoToPageAsync(Page - 1), _ => HasPreviousPage);
    }

    public ObservableCollection<BatchRowDto> Rows { get; } = new();
    public ObservableCollection<ProductDto> ProductOptions { get; } = new();
    public ObservableCollection<ExpiryFilterOption> ExpiryOptions { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand PreviousPageCommand { get; }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public ProductDto? SelectedProduct { get => _selectedProduct; set => SetProperty(ref _selectedProduct, value); }
    public ExpiryFilterOption? SelectedExpiry { get => _selectedExpiry; set => SetProperty(ref _selectedExpiry, value); }
    public BatchRowDto? SelectedBatch { get => _selectedBatch; set => SetProperty(ref _selectedBatch, value); }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public int Page
    {
        get => _page;
        private set { if (SetProperty(ref _page, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPagingCommands(); } }
    }
    public int TotalCount { get => _totalCount; private set { if (SetProperty(ref _totalCount, value)) OnPropertyChanged(nameof(PageLabel)); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPagingCommands(); } } }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
    public string PageLabel => TotalPages == 0 ? "Page 0 of 0" : $"Page {Page} of {TotalPages}  •  {TotalCount} batch(es)";

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        await LoadProductOptionsAsync();
        await SearchAsync();
    }

    private async Task LoadProductOptionsAsync()
    {
        if (ProductOptions.Count > 0) return;
        foreach (var p in await ProductLoader.LoadAsync(_products, trackedOnly: false)) ProductOptions.Add(p);
        if (SelectedExpiry is null) SelectedExpiry = ExpiryOptions[0];
    }

    public async Task ReloadAsync() => await SearchAsync();

    public async Task SearchAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            await RunQueryAsync(1);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load batches: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GoToPageAsync(int page)
    {
        if (page < 1 || page > TotalPages || page == Page) return;
        IsLoading = true;
        try
        {
            await RunQueryAsync(page);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load batches: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RunQueryAsync(int page)
    {
        var filter = new BatchFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            ProductId = SelectedProduct?.Id,
            Expiry = SelectedExpiry?.Value ?? ExpiryFilter.All,
            Page = page,
            PageSize = PageSize
        };

        var result = await _queries.GetBatchesAsync(filter);
        Rows.Clear();
        foreach (var row in result.Items) Rows.Add(row);
        TotalCount = result.TotalCount;
        TotalPages = result.TotalPages;
        Page = result.Page;
        StatusMessage = result.TotalCount == 0
            ? "No batches match the current filters."
            : $"{result.TotalCount} batch(es) found.";
    }

    public async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedProduct = null;
        SelectedExpiry = ExpiryOptions.Count > 0 ? ExpiryOptions[0] : null;
        await SearchAsync();
    }

    private void RefreshPagingCommands()
    {
        NextPageCommand.RaiseCanExecuteChanged();
        PreviousPageCommand.RaiseCanExecuteChanged();
    }

    public sealed record ExpiryFilterOption(string Label, ExpiryFilter Value);
}

/// <summary>Records a stock receipt (stock-in) that creates a new inventory batch.</summary>
public sealed class StockInViewModel : ObservableObject
{
    private readonly IInventoryService _inventory;
    private readonly IProductService _products;

    private ProductDto? _selectedProduct;
    private string _batchNumber = string.Empty;
    private DateTime? _receivedDate = DateTime.Today;
    private DateTime? _expiryDate;
    private string _quantityText = string.Empty;
    private string _unitCostText = string.Empty;
    private ReceiptReasonOption? _selectedReason;
    private string _referenceNumber = string.Empty;
    private string _note = string.Empty;
    private bool _isLoading;
    private bool _isBusy;
    private bool _isError;
    private string _statusMessage = string.Empty;

    public StockInViewModel(IInventoryService inventory, IProductService products)
    {
        _inventory = inventory;
        _products = products;

        foreach (var reason in Enum.GetValues<StockReceiptReason>())
            ReasonOptions.Add(new ReceiptReasonOption(ReceiptReasonName(reason), reason));

        SaveCommand = new RelayCommand(_ => _ = SaveAsync(), _ => !IsBusy);
        RefreshProductsCommand = new RelayCommand(_ => _ = LoadProductsAsync());
    }

    /// <summary>Invoked after a successful receipt so the hub can refresh read-only tabs.</summary>
    public Func<Task>? Saved { get; set; }

    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<ReceiptReasonOption> ReasonOptions { get; } = new();

    public RelayCommand SaveCommand { get; }
    public RelayCommand RefreshProductsCommand { get; }

    public ProductDto? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                OnPropertyChanged(nameof(ProductRequiresExpiry));
                OnPropertyChanged(nameof(ProductUnit));
            }
        }
    }

    public bool ProductRequiresExpiry => SelectedProduct?.RequiresExpiryDate ?? false;
    public bool ProductTracked => SelectedProduct?.TrackInventory ?? false;
    public string ProductUnit => SelectedProduct?.Unit ?? string.Empty;

    public string BatchNumber { get => _batchNumber; set => SetProperty(ref _batchNumber, value); }
    public DateTime? ReceivedDate { get => _receivedDate; set => SetProperty(ref _receivedDate, value); }
    public DateTime? ExpiryDate { get => _expiryDate; set => SetProperty(ref _expiryDate, value); }
    public string QuantityText { get => _quantityText; set => SetProperty(ref _quantityText, value); }
    public string UnitCostText { get => _unitCostText; set => SetProperty(ref _unitCostText, value); }
    public ReceiptReasonOption? SelectedReason { get => _selectedReason; set => SetProperty(ref _selectedReason, value); }
    public string ReferenceNumber { get => _referenceNumber; set => SetProperty(ref _referenceNumber, value); }
    public string Note { get => _note; set => SetProperty(ref _note, value); }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) SaveCommand.RaiseCanExecuteChanged(); } }
    public bool IsError { get => _isError; private set => SetProperty(ref _isError, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);

    public async Task LoadProductsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            Products.Clear();
            foreach (var p in await ProductLoader.LoadAsync(_products, trackedOnly: true)) Products.Add(p);
            if (SelectedProduct is null && Products.Count > 0) SelectedProduct = Products[0];
            if (SelectedReason is null && ReasonOptions.Count > 0) SelectedReason = ReasonOptions[0];
        }
        catch (Exception ex)
        {
            ShowError($"Could not load products: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SaveAsync()
    {
        if (SelectedProduct is null) { ShowError("Select a product to receive."); return; }
        if (string.IsNullOrWhiteSpace(BatchNumber)) { ShowError("A batch number is required."); return; }
        if (!TryParseDecimal(QuantityText, out var quantity) || quantity <= 0)
        { ShowError("Enter a quantity greater than zero."); return; }
        if (!TryParseDecimal(UnitCostText, out var unitCost) || unitCost < 0)
        { ShowError("Enter a valid unit cost (zero or more)."); return; }
        if (ProductRequiresExpiry && ExpiryDate is null)
        { ShowError("This product requires an expiry date."); return; }
        if (ExpiryDate is { } exp && ReceivedDate is { } rec && exp.Date < rec.Date)
        { ShowError("The expiry date cannot be before the received date."); return; }

        IsBusy = true;
        try
        {
            var request = new StockReceiptRequest
            {
                ProductId = SelectedProduct.Id,
                BatchNumber = BatchNumber.Trim(),
                Quantity = quantity,
                UnitCost = unitCost,
                ReceivedDate = ReceivedDate,
                ExpiryDate = ExpiryDate,
                Reason = SelectedReason?.Value ?? StockReceiptReason.ManualReceipt,
                ReferenceNumber = string.IsNullOrWhiteSpace(ReferenceNumber) ? null : ReferenceNumber.Trim(),
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
            };

            await _inventory.ReceiveAsync(request);

            BatchNumber = string.Empty;
            QuantityText = string.Empty;
            UnitCostText = string.Empty;
            ExpiryDate = null;
            ReferenceNumber = string.Empty;
            Note = string.Empty;
            ReceivedDate = DateTime.Today;

            ShowSuccess($"Received {quantity:0.###} {ProductUnit} of {SelectedProduct.Name} into batch.");
            if (Saved is not null) await Saved();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowError(string message) { IsError = true; StatusMessage = message; }
    private void ShowSuccess(string message) { IsError = false; StatusMessage = message; }

    private static bool TryParseDecimal(string? text, out decimal value)
        => decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out value)
           || decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);

    private static string ReceiptReasonName(StockReceiptReason reason) => reason switch
    {
        StockReceiptReason.OpeningStock => "Opening stock",
        StockReceiptReason.ManualReceipt => "Manual receipt",
        StockReceiptReason.PositiveCorrection => "Positive correction",
        _ => reason.ToString()
    };

    public sealed record ReceiptReasonOption(string Label, StockReceiptReason Value);
}

/// <summary>Applies a manual stock adjustment (correction / damaged / expired) to a batch.</summary>
public sealed class StockAdjustmentViewModel : ObservableObject
{
    private readonly IInventoryService _inventory;
    private readonly IProductService _products;

    private ProductDto? _selectedProduct;
    private BatchDto? _selectedBatch;
    private AdjustmentKindOption? _selectedKind;
    private string _quantityText = string.Empty;
    private string _unitCostText = string.Empty;
    private string _referenceNumber = string.Empty;
    private string _note = string.Empty;
    private bool _isLoading;
    private bool _isBusy;
    private bool _isError;
    private string _statusMessage = string.Empty;

    public StockAdjustmentViewModel(IInventoryService inventory, IProductService products)
    {
        _inventory = inventory;
        _products = products;

        KindOptions.Add(new AdjustmentKindOption("Correction (+/−)", StockAdjustmentKind.Correction));
        KindOptions.Add(new AdjustmentKindOption("Damaged", StockAdjustmentKind.Damaged));
        KindOptions.Add(new AdjustmentKindOption("Expired", StockAdjustmentKind.Expired));

        SaveCommand = new RelayCommand(_ => _ = SaveAsync(), _ => !IsBusy);
        RefreshProductsCommand = new RelayCommand(_ => _ = LoadProductsAsync());
    }

    public Func<Task>? Saved { get; set; }

    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<BatchDto> Batches { get; } = new();
    public ObservableCollection<AdjustmentKindOption> KindOptions { get; } = new();

    public RelayCommand SaveCommand { get; }
    public RelayCommand RefreshProductsCommand { get; }

    public ProductDto? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                _ = LoadBatchesAsync();
            }
        }
    }

    public BatchDto? SelectedBatch { get => _selectedBatch; set => SetProperty(ref _selectedBatch, value); }
    public AdjustmentKindOption? SelectedKind { get => _selectedKind; set => SetProperty(ref _selectedKind, value); }
    public string QuantityText { get => _quantityText; set => SetProperty(ref _quantityText, value); }
    public string UnitCostText { get => _unitCostText; set => SetProperty(ref _unitCostText, value); }
    public string ReferenceNumber { get => _referenceNumber; set => SetProperty(ref _referenceNumber, value); }
    public string Note { get => _note; set => SetProperty(ref _note, value); }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) SaveCommand.RaiseCanExecuteChanged(); } }
    public bool IsError { get => _isError; private set => SetProperty(ref _isError, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);

    public async Task LoadProductsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            Products.Clear();
            foreach (var p in await ProductLoader.LoadAsync(_products, trackedOnly: true)) Products.Add(p);
            if (SelectedProduct is null && Products.Count > 0) SelectedProduct = Products[0];
            if (SelectedKind is null && KindOptions.Count > 0) SelectedKind = KindOptions[0];
        }
        catch (Exception ex)
        {
            ShowError($"Could not load products: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadBatchesAsync()
    {
        Batches.Clear();
        SelectedBatch = null;
        if (SelectedProduct is null) return;
        try
        {
            var batches = await _inventory.GetBatchesAsync(SelectedProduct.Id);
            foreach (var b in batches) Batches.Add(b);
            if (Batches.Count > 0) SelectedBatch = Batches[0];
        }
        catch (Exception ex)
        {
            ShowError($"Could not load batches: {ex.Message}");
        }
    }

    public async Task SaveAsync()
    {
        if (SelectedProduct is null) { ShowError("Select a product."); return; }
        if (!TryParseDecimal(QuantityText, out var quantity) || quantity == 0)
        { ShowError("Enter a non-zero quantity change."); return; }

        var kind = SelectedKind?.Value ?? StockAdjustmentKind.Correction;
        var unitCost = 0m;
        if (TryParseDecimal(UnitCostText, out var parsedCost))
            unitCost = parsedCost;
        else if (!string.IsNullOrWhiteSpace(UnitCostText))
        { ShowError("Enter a valid unit cost (zero or more)."); return; }

        // Damaged / expired always reduce stock regardless of the sign entered.
        var change = kind is StockAdjustmentKind.Damaged or StockAdjustmentKind.Expired
            ? -Math.Abs(quantity)
            : quantity;

        IsBusy = true;
        try
        {
            var line = new StockAdjustmentLine
            {
                ProductId = SelectedProduct.Id,
                InventoryBatchId = SelectedBatch?.Id,
                QuantityChange = change,
                UnitCost = unitCost,
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                Kind = kind
            };

            var request = new StockAdjustmentRequest
            {
                ReferenceNumber = string.IsNullOrWhiteSpace(ReferenceNumber) ? "ADJ" : ReferenceNumber.Trim(),
                Reason = SelectedKind?.Label ?? "Stock adjustment",
                Notes = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                Lines = new List<StockAdjustmentLine> { line }
            };

            await _inventory.AdjustAsync(request);

            QuantityText = string.Empty;
            UnitCostText = string.Empty;
            ReferenceNumber = string.Empty;
            Note = string.Empty;

            ShowSuccess($"Applied {change:+0.###;-0.###} {SelectedProduct.Unit} adjustment to {SelectedProduct.Name}.");
            if (Saved is not null) await Saved();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowError(string message) { IsError = true; StatusMessage = message; }
    private void ShowSuccess(string message) { IsError = false; StatusMessage = message; }

    private static bool TryParseDecimal(string? text, out decimal value)
        => decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out value)
           || decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);

    public sealed record AdjustmentKindOption(string Label, StockAdjustmentKind Value);
}

/// <summary>Immutable stock movement ledger with search, type and date filters.</summary>
public sealed class StockMovementViewModel : ObservableObject
{
    private readonly IInventoryQueryService _queries;
    private readonly IProductService _products;
    private const int PageSize = 20;

    private string _searchText = string.Empty;
    private ProductDto? _selectedProduct;
    private MovementTypeOption? _selectedType;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private MovementRowDto? _selectedRow;
    private int _page = 1;
    private int _totalCount;
    private int _totalPages;
    private bool _isLoading;
    private string _statusMessage = "Loading movements…";

    public StockMovementViewModel(IInventoryQueryService queries, IProductService products)
    {
        _queries = queries;
        _products = products;

        TypeOptions.Add(new MovementTypeOption("All types", null));
        foreach (var type in Enum.GetValues<StockMovementType>())
            TypeOptions.Add(new MovementTypeOption(type.ToString(), type));

        RefreshCommand = new RelayCommand(_ => _ = ReloadAsync());
        SearchCommand = new RelayCommand(_ => _ = SearchAsync());
        ClearFiltersCommand = new RelayCommand(_ => _ = ClearFiltersAsync());
        NextPageCommand = new RelayCommand(_ => _ = GoToPageAsync(Page + 1), _ => HasNextPage);
        PreviousPageCommand = new RelayCommand(_ => _ = GoToPageAsync(Page - 1), _ => HasPreviousPage);
    }

    public ObservableCollection<MovementRowDto> Rows { get; } = new();
    public ObservableCollection<ProductDto> ProductOptions { get; } = new();
    public ObservableCollection<MovementTypeOption> TypeOptions { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand PreviousPageCommand { get; }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public ProductDto? SelectedProduct { get => _selectedProduct; set => SetProperty(ref _selectedProduct, value); }
    public MovementTypeOption? SelectedType { get => _selectedType; set => SetProperty(ref _selectedType, value); }
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public MovementRowDto? SelectedRow { get => _selectedRow; set => SetProperty(ref _selectedRow, value); }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public int Page
    {
        get => _page;
        private set { if (SetProperty(ref _page, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPagingCommands(); } }
    }
    public int TotalCount { get => _totalCount; private set { if (SetProperty(ref _totalCount, value)) OnPropertyChanged(nameof(PageLabel)); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPagingCommands(); } } }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
    public string PageLabel => TotalPages == 0 ? "Page 0 of 0" : $"Page {Page} of {TotalPages}  •  {TotalCount} movement(s)";

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        await LoadProductOptionsAsync();
        await SearchAsync();
    }

    private async Task LoadProductOptionsAsync()
    {
        if (ProductOptions.Count > 0) return;
        foreach (var p in await ProductLoader.LoadAsync(_products, trackedOnly: false)) ProductOptions.Add(p);
        if (SelectedType is null) SelectedType = TypeOptions[0];
    }

    public async Task ReloadAsync() => await SearchAsync();

    public async Task SearchAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            await RunQueryAsync(1);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load movements: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GoToPageAsync(int page)
    {
        if (page < 1 || page > TotalPages || page == Page) return;
        IsLoading = true;
        try
        {
            await RunQueryAsync(page);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load movements: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RunQueryAsync(int page)
    {
        var filter = new MovementFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            ProductId = SelectedProduct?.Id,
            MovementType = SelectedType?.Value,
            From = FromDate,
            To = ToDate,
            Page = page,
            PageSize = PageSize
        };

        var result = await _queries.GetMovementsAsync(filter);
        Rows.Clear();
        foreach (var row in result.Items) Rows.Add(row);
        TotalCount = result.TotalCount;
        TotalPages = result.TotalPages;
        Page = result.Page;
        StatusMessage = result.TotalCount == 0
            ? "No movements match the current filters."
            : $"{result.TotalCount} movement(s) found.";
    }

    public async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedProduct = null;
        SelectedType = TypeOptions.Count > 0 ? TypeOptions[0] : null;
        FromDate = null;
        ToDate = null;
        await SearchAsync();
    }

    private void RefreshPagingCommands()
    {
        NextPageCommand.RaiseCanExecuteChanged();
        PreviousPageCommand.RaiseCanExecuteChanged();
    }

    public sealed record MovementTypeOption(string Label, StockMovementType? Value);
}

/// <summary>Near-expiry and expired batch lists.</summary>
public sealed class ExpiryViewModel : ObservableObject
{
    private readonly IInventoryQueryService _queries;
    private bool _isLoading;
    private string _statusMessage = "Loading expiry data…";

    public ExpiryViewModel(IInventoryQueryService queries) => _queries = queries;

    public ObservableCollection<ExpiryRowDto> NearExpiry { get; } = new();
    public ObservableCollection<ExpiryRowDto> Expired { get; } = new();

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public int NearExpiryCount => NearExpiry.Count;
    public int ExpiredCount => Expired.Count;

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var near = await _queries.GetNearExpiryAsync(ExpiryRules.UpcomingDays);
            NearExpiry.Clear();
            foreach (var row in near) NearExpiry.Add(row);

            var expired = await _queries.GetExpiredAsync();
            Expired.Clear();
            foreach (var row in expired) Expired.Add(row);

            OnPropertyChanged(nameof(NearExpiryCount));
            OnPropertyChanged(nameof(ExpiredCount));

            StatusMessage = near.Count == 0 && expired.Count == 0
                ? "No batches are near expiry or expired."
                : $"{near.Count} batch(es) expiring soon • {expired.Count} expired batch(es).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load expiry data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}