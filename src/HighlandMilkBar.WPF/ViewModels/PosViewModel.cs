using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>A single line in the POS shopping cart.</summary>
public sealed class CartLine : ObservableObject
{
    private decimal _quantity;
    private decimal _discount;

    public int ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public bool IsTracked { get; init; }
    public decimal Available { get; init; }

    public decimal Quantity
    {
        get => _quantity;
        set { if (SetProperty(ref _quantity, value)) OnPropertyChanged(nameof(LineTotal)); }
    }

    public decimal Discount
    {
        get => _discount;
        set { if (SetProperty(ref _discount, value)) OnPropertyChanged(nameof(LineTotal)); }
    }

    public decimal LineTotal => Math.Max(0m, Quantity * UnitPrice - Discount);
    public string AvailabilityText => IsTracked ? $"In stock: {Available:0.###}" : "Made to order";
}

/// <summary>
/// Point-of-sale terminal: product browsing, cart, discounts and payment/checkout. Stock is only
/// deducted at checkout (never when items are added to the cart).
/// </summary>
public sealed class PosViewModel : ObservableObject
{
    private readonly IProductService _products;
    private readonly IInventoryQueryService _inventory;
    private readonly ISalesService _sales;
    private readonly ISalesQueryService _salesQuery;
    private readonly IReceiptService _receipts;

    private readonly Dictionary<int, decimal> _sellable = new();
    private string _searchText = string.Empty;
    private CategoryDto? _selectedCategory;
    private CartLine? _selectedLine;
    private string _orderDiscountText = string.Empty;
    private PosPaymentMethod _paymentMethod = PosPaymentMethod.Cash;
    private string _cashReceivedText = string.Empty;
    private string _cardAmountText = string.Empty;
    private bool _isProcessing;
    private bool _isError;
    private string _statusMessage = "Ready.";
    private string _lastReceiptNumber = string.Empty;
    private string _customerSearchText = string.Empty;
    private CustomerOptionDto? _selectedCustomer;

    public PosViewModel(IProductService products, IInventoryQueryService inventory, ISalesService sales, ISalesQueryService salesQuery, IReceiptService receipts)
    {
        _products = products;
        _inventory = inventory;
        _sales = sales;
        _salesQuery = salesQuery;
        _receipts = receipts;

        AddCommand = new RelayCommand(p => AddToCart(p as ProductDto), p => p is ProductDto);
        IncreaseCommand = new RelayCommand(_ => ChangeSelected(1));
        DecreaseCommand = new RelayCommand(_ => ChangeSelected(-1));
        RemoveCommand = new RelayCommand(_ => RemoveSelected());
        ClearCommand = new RelayCommand(_ => Cart.Clear());
        CheckoutCommand = new RelayCommand(_ => _ = CheckoutAsync(), _ => !IsProcessing && Cart.Count > 0);

        SearchCommand = new RelayCommand(_ => ApplyFilters());
        CategoryChangedCommand = new RelayCommand(_ => ApplyFilters());
        SearchCustomersCommand = new RelayCommand(_ => _ = SearchCustomersAsync());
        SelectCustomerCommand = new RelayCommand(p => SelectCustomer(p as CustomerOptionDto));
        RemoveCustomerCommand = new RelayCommand(_ => RemoveCustomer());
    }

    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<ProductDto> FilteredProducts { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<CartLine> Cart { get; } = new();
    public ObservableCollection<CustomerOptionDto> CustomerResults { get; } = new();

    public Array PaymentMethods => Enum.GetValues<PosPaymentMethod>();

    public RelayCommand AddCommand { get; }
    public RelayCommand IncreaseCommand { get; }
    public RelayCommand DecreaseCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand CheckoutCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand CategoryChangedCommand { get; }
    public RelayCommand SearchCustomersCommand { get; }
    public RelayCommand SelectCustomerCommand { get; }
    public RelayCommand RemoveCustomerCommand { get; }

    /// <summary>Raised after a successful checkout so the view can open the receipt.</summary>
    public event Action<int>? SaleCompleted;

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public CategoryDto? SelectedCategory { get => _selectedCategory; set { if (SetProperty(ref _selectedCategory, value)) ApplyFilters(); } }
    public CartLine? SelectedLine { get => _selectedLine; set => SetProperty(ref _selectedLine, value); }
    public string OrderDiscountText { get => _orderDiscountText; set { if (SetProperty(ref _orderDiscountText, value)) Recalculate(); } }

    // ---- Optional customer selection (walk-in when none is selected) ----
    public string CustomerSearchText
    {
        get => _customerSearchText;
        set { if (SetProperty(ref _customerSearchText, value)) { } }
    }

    public CustomerOptionDto? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                OnPropertyChanged(nameof(HasCustomer));
                OnPropertyChanged(nameof(CustomerDisplayName));
                OnPropertyChanged(nameof(CustomerDisplayPhone));
                OnPropertyChanged(nameof(CustomerStatusText));
            }
        }
    }

    public bool HasCustomer => _selectedCustomer is not null;
    public string CustomerDisplayName => _selectedCustomer?.Name ?? "Walk-in Customer";
    public string CustomerDisplayPhone => _selectedCustomer?.Phone ?? string.Empty;
    public string CustomerStatusText => _selectedCustomer is null ? "Walk-in" : "Customer selected";

    public PosPaymentMethod SelectedPaymentMethod
    {
        get => _paymentMethod;
        set
        {
            if (SetProperty(ref _paymentMethod, value))
            {
                OnPropertyChanged(nameof(IsSplit));
                OnPropertyChanged(nameof(ShowCashInput));
                OnPropertyChanged(nameof(ShowCardInput));
                Recalculate();
            }
        }
    }

    public bool IsSplit => _paymentMethod == PosPaymentMethod.Split;
    public bool ShowCashInput => _paymentMethod is PosPaymentMethod.Cash or PosPaymentMethod.Split;
    public bool ShowCardInput => _paymentMethod == PosPaymentMethod.Split;

    public string CashReceivedText { get => _cashReceivedText; set { if (SetProperty(ref _cashReceivedText, value)) Recalculate(); } }
    public string CardAmountText { get => _cardAmountText; set { if (SetProperty(ref _cardAmountText, value)) Recalculate(); } }

    public bool IsProcessing { get => _isProcessing; private set { if (SetProperty(ref _isProcessing, value)) CheckoutCommand.RaiseCanExecuteChanged(); } }
    public bool IsError { get => _isError; private set => SetProperty(ref _isError, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);

    public int CartCount => Cart.Count;
    public decimal CartQuantity => Cart.Sum(l => l.Quantity);
    public decimal Subtotal => Cart.Sum(l => l.Quantity * l.UnitPrice);
    public decimal LineDiscount => Cart.Sum(l => l.Discount);
    public decimal OrderDiscount => ParseDecimal(OrderDiscountText);
    public decimal Total => Math.Max(0m, Subtotal - LineDiscount - OrderDiscount);
    public decimal CardTendered => SelectedPaymentMethod switch
    {
        PosPaymentMethod.Card => Total,
        PosPaymentMethod.Split => ParseDecimal(CardAmountText),
        _ => 0m
    };
    public decimal CashTendered => SelectedPaymentMethod switch
    {
        PosPaymentMethod.Cash => ParseDecimal(CashReceivedText),
        PosPaymentMethod.Split => ParseDecimal(CashReceivedText),
        _ => 0m
    };
    public decimal AmountPaid => CardTendered + CashTendered;
    public decimal Change => Math.Max(0m, AmountPaid - Total);
    public bool IsPaymentSufficient => AmountPaid + 0.001m >= Total;

    public async Task LoadAsync()
    {
        if (IsProcessing) return;
        IsProcessing = true;
        try
        {
            await LoadCategoriesAsync();
            await LoadProductsAsync();
            await LoadStockAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Could not load the catalogue: {ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _products.GetCategoriesAsync();
        Categories.Clear();
        Categories.Add(new CategoryDto { Id = 0, Name = "All categories" });
        foreach (var c in categories) Categories.Add(c);
        SelectedCategory = Categories[0];
    }

    private async Task LoadProductsAsync()
    {
        var all = new List<ProductDto>();
        var page = 1;
        while (true)
        {
            var result = await _products.SearchAsync(new ProductFilter { IsActive = true, Page = page, PageSize = 200 });
            all.AddRange(result.Items);
            if (result.TotalPages == 0 || page >= result.TotalPages || result.Items.Count == 0) break;
            page++;
        }

        Products.Clear();
        foreach (var p in all) Products.Add(p);
        ApplyFilters();
    }

    private async Task LoadStockAsync()
    {
        _sellable.Clear();
        var page = 1;
        while (true)
        {
            var result = await _inventory.GetStockAsync(new StockFilter { Page = page, PageSize = 200 });
            foreach (var row in result.Items) _sellable[row.ProductId] = row.SellableStock;
            if (result.TotalPages == 0 || page >= result.TotalPages || result.Items.Count == 0) break;
            page++;
        }
    }

    private void ApplyFilters()
    {
        var search = SearchText?.Trim();
        IEnumerable<ProductDto> query = Products;
        if (SelectedCategory is { Id: > 0 } c)
            query = query.Where(p => p.CategoryId == c.Id);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search;
            query = query.Where(p =>
                p.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (p.Barcode?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        FilteredProducts.Clear();
        foreach (var p in query.OrderBy(p => p.Name)) FilteredProducts.Add(p);
        OnPropertyChanged(nameof(FilterCount));
    }

    public int FilterCount => FilteredProducts.Count;

    private async Task SearchCustomersAsync()
    {
        var results = await _salesQuery.SearchCustomersAsync(string.IsNullOrWhiteSpace(CustomerSearchText) ? null : CustomerSearchText.Trim());
        CustomerResults.Clear();
        foreach (var c in results) CustomerResults.Add(c);
    }

    private void SelectCustomer(CustomerOptionDto? customer)
    {
        if (customer is null) return;
        SelectedCustomer = customer;
        CustomerSearchText = string.Empty;
        CustomerResults.Clear();
        ClearError();
    }

    private void RemoveCustomer()
    {
        SelectedCustomer = null;
        CustomerSearchText = string.Empty;
        CustomerResults.Clear();
    }

    private decimal AvailableFor(ProductDto p) => p.TrackInventory && _sellable.TryGetValue(p.Id, out var qty) ? qty : decimal.MaxValue;

    private void AddToCart(ProductDto? product)
    {
        if (product is null) return;

        var available = AvailableFor(product);
        if (available <= 0)
        {
            ShowError($"'{product.Name}' is out of stock.");
            return;
        }

        var line = Cart.FirstOrDefault(l => l.ProductId == product.Id);
        if (line is null)
        {
            Cart.Add(new CartLine
            {
                ProductId = product.Id,
                Name = product.Name,
                Unit = product.Unit,
                UnitPrice = product.SellingPrice,
                IsTracked = product.TrackInventory,
                Available = available
            });
        }
        else
        {
            if (line.IsTracked && line.Quantity + 1 > line.Available)
            {
                ShowError($"Only {line.Available:0.###} of '{line.Name}' available.");
                return;
            }
            line.Quantity += 1;
        }

        ClearError();
        RefreshTotals();
    }

    private void ChangeSelected(int delta)
    {
        if (SelectedLine is null) return;
        var target = SelectedLine.Quantity + delta;
        if (target < 0) target = 0;
        if (SelectedLine.IsTracked && target > SelectedLine.Available)
        {
            ShowError($"Only {SelectedLine.Available:0.###} of '{SelectedLine.Name}' available.");
            target = SelectedLine.Available;
        }
        SelectedLine.Quantity = target;
        RefreshTotals();
    }

    private void RemoveSelected()
    {
        if (SelectedLine is null) return;
        Cart.Remove(SelectedLine);
        SelectedLine = null;
        RefreshTotals();
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(CartCount));
        OnPropertyChanged(nameof(CartQuantity));
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(LineDiscount));
        OnPropertyChanged(nameof(Total));
        Recalculate();
    }

    private void Recalculate()
    {
        OnPropertyChanged(nameof(OrderDiscount));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(CardTendered));
        OnPropertyChanged(nameof(CashTendered));
        OnPropertyChanged(nameof(AmountPaid));
        OnPropertyChanged(nameof(Change));
        OnPropertyChanged(nameof(IsPaymentSufficient));
    }

    public async Task CheckoutAsync()
    {
        if (IsProcessing) return;
        if (Cart.Count == 0) { ShowError("The cart is empty."); return; }

        var items = Cart.Select(l => new SaleLineRequest
        {
            ProductId = l.ProductId,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            DiscountAmount = l.Discount
        }).ToList();

        var payments = BuildPayments();
        if (payments.Count == 0) { ShowError("Enter payment details."); return; }

        var nonCash = payments.Where(p => p.Method != PaymentMethod.Cash).Sum(p => p.Amount);
        if (nonCash > Total + 0.001m) { ShowError("Card amount cannot exceed the sale total."); return; }
        if (AmountPaid + 0.001m < Total) { ShowError("Payment is less than the total due."); return; }

        var request = new SaleCheckoutRequest
        {
            CustomerId = SelectedCustomer?.Id,
            Items = items,
            OrderDiscount = OrderDiscount,
            Payments = payments,
            AllocationPolicy = AllocationPolicy.Fefo
        };

        IsProcessing = true;
        try
        {
            var result = await _sales.CheckoutAsync(request);
            LastCompletedSaleId = result.SaleId;
            LastReceiptNumber = result.ReceiptNumber;
            try
            {
                LastReceipt = await _receipts.CreateForSaleAsync(result.SaleId, ReceiptFormat.Thermal80);
            }
            catch
            {
                // A receipt failure must never undo a committed sale; it can be reprinted later.
                LastReceipt = null;
            }

            Cart.Clear();
            SelectedLine = null;
            CashReceivedText = string.Empty;
            CardAmountText = string.Empty;
            OrderDiscountText = string.Empty;
            RemoveCustomer();
            RefreshTotals();

            ShowSuccess($"Sale {result.ReceiptNumber} completed. Change: {result.Change:0.00}.");
            SaleCompleted?.Invoke(result.SaleId);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    public int? LastCompletedSaleId { get; private set; }
    public string LastReceiptNumber { get; private set; } = string.Empty;

    /// <summary>The most recently built receipt document, ready for preview/print/PDF.</summary>
    public ReceiptDocument? LastReceipt { get; private set; }

    private List<PaymentRequest> BuildPayments()
    {
        var payments = new List<PaymentRequest>();
        switch (SelectedPaymentMethod)
        {
            case PosPaymentMethod.Cash:
                var cash = ParseDecimal(CashReceivedText);
                if (cash > 0) payments.Add(new PaymentRequest { Method = PaymentMethod.Cash, Amount = cash });
                break;
            case PosPaymentMethod.Card:
                payments.Add(new PaymentRequest { Method = PaymentMethod.Card, Amount = Total });
                break;
            case PosPaymentMethod.Split:
                var splitCash = ParseDecimal(CashReceivedText);
                var splitCard = ParseDecimal(CardAmountText);
                if (splitCash > 0) payments.Add(new PaymentRequest { Method = PaymentMethod.Cash, Amount = splitCash });
                if (splitCard > 0) payments.Add(new PaymentRequest { Method = PaymentMethod.Card, Amount = splitCard });
                break;
        }
        return payments;
    }

    private static decimal ParseDecimal(string? text)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var v) ? v
            : decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var v2) ? v2 : 0m;

    private void ShowError(string message) { IsError = true; StatusMessage = message; }
    private void ShowSuccess(string message) { IsError = false; StatusMessage = message; }
    private void ClearError() { IsError = false; StatusMessage = string.Empty; }
}

public enum PosPaymentMethod
{
    Cash,
    Card,
    Split
}