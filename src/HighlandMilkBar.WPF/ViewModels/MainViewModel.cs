using System.Collections.ObjectModel;
using System.Windows.Input;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IBusinessAnalyticsQueryService _analytics;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IInventoryService _inventoryService;
    private readonly IInventoryQueryService _inventoryQueryService;
    private readonly ISalesService _salesService;
    private readonly ISalesQueryService _salesQueryService;
    private readonly IReceiptService _receiptService;
    private readonly IMilkBarService _milkBarService;
    private readonly IRecipeService _recipeService;
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierService _supplierService;
    private readonly ISupplierPaymentService _supplierPaymentService;
    private readonly ICustomerService _customerService;
    private readonly ICustomerQueryService _customerQueryService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IExpenseService _expenseService;
    private readonly IExpenseQueryService _expenseQueryService;
    private readonly IFinancialQueryService _financialQueryService;
    private object? _currentViewModel;
    private string _currentTitle = "Dashboard";
    private string _currentSubtitle = "Your outlet at a glance";
    private string _searchText = string.Empty;
    private bool _isSidebarCollapsed;
    private double _sidebarWidth = 248;

    public MainViewModel(
        IBusinessAnalyticsQueryService analytics,
        IProductService productService,
        ICategoryService categoryService,
        IInventoryService inventoryService,
        IInventoryQueryService inventoryQueryService,
        ISalesService salesService,
        ISalesQueryService salesQueryService,
        IReceiptService receiptService,
        IMilkBarService milkBarService,
        IRecipeService recipeService,
        IPurchaseService purchaseService,
        ISupplierService supplierService,
        ISupplierPaymentService supplierPaymentService,
        ICustomerService customerService,
        ICustomerQueryService customerQueryService,
        ILoyaltyService loyaltyService,
        IExpenseService expenseService,
        IExpenseQueryService expenseQueryService,
        IFinancialQueryService financialQueryService)
    {
        _analytics = analytics;
        _productService = productService;
        _categoryService = categoryService;
        _inventoryService = inventoryService;
        _inventoryQueryService = inventoryQueryService;
        _salesService = salesService;
        _salesQueryService = salesQueryService;
        _receiptService = receiptService;
        _milkBarService = milkBarService;
        _recipeService = recipeService;
        _purchaseService = purchaseService;
        _supplierService = supplierService;
        _supplierPaymentService = supplierPaymentService;
        _customerService = customerService;
        _customerQueryService = customerQueryService;
        _loyaltyService = loyaltyService;
        _expenseService = expenseService;
        _expenseQueryService = expenseQueryService;
        _financialQueryService = financialQueryService;

        NavItems = new ObservableCollection<NavItemViewModel>
        {
            new("Dashboard", "⌂", "Dashboard"),
            new("Analytics", "◔", "Analytics"),
            new("Point of Sale", "▣", "Point of Sale"),
            new("Sales History", "☰", "Sales History"),
            new("Products", "▤", "Products"),
            new("Inventory", "▥", "Inventory"),
            new("Milk Bar", "◍", "Milk Bar"),
            new("Purchases", "⇩", "Purchases"),
            new("Suppliers", "♧", "Suppliers"),
            new("Customers", "♙", "Customers"),
            new("Expenses", "◈", "Expenses"),
            new("Finance", "＄", "Finance"),
            new("Reports", "▧", "Reports"),
            new("Settings", "⚙", "Settings")
        };

        NavigateCommand = new RelayCommand(parameter =>
        {
            if (parameter is NavItemViewModel item) NavigateTo(item.Key);
        });

        ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);
        OpenSettingsCommand = new RelayCommand(_ => NavigateTo("Settings"));

        NavigateTo("Dashboard");
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; }
    public ICommand NavigateCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public object? CurrentViewModel { get => _currentViewModel; private set => SetProperty(ref _currentViewModel, value); }
    public string CurrentTitle { get => _currentTitle; private set => SetProperty(ref _currentTitle, value); }
    public string CurrentSubtitle { get => _currentSubtitle; private set => SetProperty(ref _currentSubtitle, value); }
    public string TodayLabel => DateTime.Now.ToString("dddd, dd MMMM yyyy");

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            if (SetProperty(ref _isSidebarCollapsed, value))
            {
                SidebarWidth = value ? 76 : 248;
                OnPropertyChanged(nameof(SidebarGlyph));
            }
        }
    }

    public double SidebarWidth
    {
        get => _sidebarWidth;
        private set => SetProperty(ref _sidebarWidth, value);
    }

    public string SidebarGlyph => IsSidebarCollapsed ? "»" : "«";

    public async Task InitializeAsync()
    {
        if (CurrentViewModel is DashboardViewModel dashboard)
            await dashboard.LoadAsync();
    }

    private void NavigateTo(string key)
    {
        foreach (var item in NavItems) item.IsSelected = item.Key == key;

        CurrentTitle = key;
        CurrentSubtitle = key switch
        {
            "Dashboard" => "Your outlet at a glance",
            "Analytics" => "Business-wide performance insights",
            "Point of Sale" => "Fast checkout and billing workspace",
            "Sales History" => "Past transactions, receipts and returns",
            "Products" => "Manage your product catalogue and prices",
            "Inventory" => "Stock levels, batches and expiry control",
            "Milk Bar" => "Menu items, recipes and ingredient usage",
            "Purchases" => "Supplier purchases and goods received",
            "Suppliers" => "Supplier directory and account details",
            "Customers" => "Customer profiles and purchase history",
            "Expenses" => "Record and review outlet expenses",
            "Finance" => "Revenue, costs and profitability",
            "Reports" => "Business reports and performance insights",
            "Settings" => "Application preferences and data protection",
            _ => "Outlet management"
        };

        CurrentViewModel = key switch
        {
            "Dashboard" => new DashboardViewModel(_analytics, NavigateTo),
            "Analytics" => new AnalyticsViewModel(_analytics),
            "Point of Sale" => new PosViewModel(_productService, _inventoryQueryService, _salesService, _salesQueryService, _receiptService),
            "Sales History" => new SalesHistoryViewModel(_salesQueryService, _salesService, _receiptService),
            "Products" => new ProductsViewModel(_productService, _categoryService),
            "Inventory" => new InventoryViewModel(_inventoryQueryService, _inventoryService, _productService),
            "Milk Bar" => new MilkBarViewModel(_milkBarService, _recipeService, _productService),
            "Purchases" => new PurchasesViewModel(_purchaseService, _supplierService, _productService, _supplierPaymentService),
            "Suppliers" => new SuppliersViewModel(_supplierService, _supplierPaymentService, _purchaseService),
            "Customers" => new CustomersViewModel(_customerService, _customerQueryService, _loyaltyService),
            "Expenses" => new ExpensesViewModel(_expenseService, _expenseQueryService),
            "Finance" => new FinancialViewModel(_financialQueryService),
            _ => new PlaceholderViewModel(key, CurrentSubtitle, GetScaffoldDescription(key), GetPlannedStep(key), GetGlyph(key))
        };

        if (CurrentViewModel is DashboardViewModel dashboard)
            _ = dashboard.LoadAsync();
        if (CurrentViewModel is AnalyticsViewModel analytics)
            _ = analytics.LoadAsync();
        if (CurrentViewModel is ProductsViewModel products)
            _ = products.LoadAsync();
        if (CurrentViewModel is PurchasesViewModel purchases)
            _ = purchases.LoadAsync();
        if (CurrentViewModel is SuppliersViewModel suppliers)
            _ = suppliers.LoadAsync();
        if (CurrentViewModel is CustomersViewModel customers)
            _ = customers.LoadAsync();
        if (CurrentViewModel is ExpensesViewModel expenses)
            _ = expenses.LoadAsync();
        if (CurrentViewModel is FinancialViewModel financial)
            _ = financial.LoadAsync();
    }

    private static string GetPlannedStep(string key) => key switch
    {
        "Point of Sale" => "Step 06",
        "Inventory" => "Step 05",
        "Milk Bar" => "Step 07",
        "Purchases" => "Step 08",
        "Suppliers" => "Step 08",
        "Customers" => "Step 09",
        "Expenses" => "Step 10",
        "Finance" => "Step 11",
        "Reports" => "Step 13",
        "Settings" => "Step 14",
        _ => "Later phase"
    };

    private static string GetGlyph(string key) => key switch
    {
        "Point of Sale" => "▣",
        "Inventory" => "▥",
        "Milk Bar" => "◍",
        "Purchases" => "⇩",
        "Suppliers" => "♧",
        "Customers" => "♙",
        "Expenses" => "◈",
        "Finance" => "＄",
        "Reports" => "▧",
        "Settings" => "⚙",
        _ => "✦"
    };

    private static string GetScaffoldDescription(string key) => key switch
    {
        "Point of Sale" => "The checkout workspace is reserved for product search, cart management, discounts, split payments, receipt printing and sale returns. The database service layer already supports atomic checkout; this screen will be built on top of it.",
        "Inventory" => "This workspace will manage stock-in and stock-out, stock adjustments, batch numbers, expiry dates, FIFO/FEFO allocation, damaged goods and stock movement history. The inventory services are already implemented.",
        "Milk Bar" => "This workspace will manage prepared menu items, recipes and ingredient consumption. Selling a prepared item will deduct its recipe ingredients from stock using the recipe services.",
        "Purchases" => "This workspace will record supplier invoices, purchased quantities, batch details, expiry dates, purchase costs and goods received. Purchase receiving is already implemented.",
        "Suppliers" => "This workspace will maintain supplier contact details, purchase history and outstanding supplier balances.",
        "Customers" => "This workspace will maintain optional customer profiles and customer purchase history.",
        "Expenses" => "This workspace will record operating expenses such as utilities, transport, packaging, maintenance and other costs.",
        "Finance" => "This workspace will present revenue, cost of goods sold, gross profit, operating expenses, net profit and cash-flow summaries. Revenue is never treated as profit.",
        "Reports" => "This workspace will produce daily and monthly sales, product, inventory, expiry, purchase, expense and profit-and-loss reports with PDF and Excel export.",
        "Settings" => "This workspace will contain outlet details, receipt settings, database location, backup and restore tools, and application preferences.",
        _ => "This workspace is included in the application plan and will be implemented in a later phase."
    };
}