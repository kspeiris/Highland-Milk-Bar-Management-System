using System.Collections.ObjectModel;
using System.Windows.Input;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly HighlandMilkBar.Application.Services.IProductService _productService;
    private object? _currentViewModel;
    private string _currentTitle = "Dashboard";
    private string _currentSubtitle = "Your outlet at a glance";

    public MainViewModel(IDashboardService dashboardService, HighlandMilkBar.Application.Services.IProductService productService)
    {
        _dashboardService = dashboardService;
        _productService = productService;

        NavItems = new ObservableCollection<NavItemViewModel>
        {
            new("Dashboard", "⌂", "Dashboard"),
            new("Point of Sale", "▣", "Point of Sale"),
            new("Products", "▤", "Products"),
            new("Inventory", "▥", "Inventory"),
            new("Milk Bar", "☕", "Milk Bar"),
            new("Purchases", "⇩", "Purchases"),
            new("Suppliers", "♧", "Suppliers"),
            new("Customers", "♙", "Customers"),
            new("Expenses", "◈", "Expenses"),
            new("Finance", "＄", "Finance"),
            new("Reports", "▥", "Reports"),
            new("Settings", "⚙", "Settings")
        };

        NavigateCommand = new RelayCommand(parameter =>
        {
            if (parameter is NavItemViewModel item) NavigateTo(item.Key);
        });

        NavigateTo("Dashboard");
    }

    public ObservableCollection<NavItemViewModel> NavItems { get; }
    public ICommand NavigateCommand { get; }
    public object? CurrentViewModel { get => _currentViewModel; private set => SetProperty(ref _currentViewModel, value); }
    public string CurrentTitle { get => _currentTitle; private set => SetProperty(ref _currentTitle, value); }
    public string CurrentSubtitle { get => _currentSubtitle; private set => SetProperty(ref _currentSubtitle, value); }
    public string TodayLabel => DateTime.Now.ToString("dddd, dd MMMM yyyy");

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
            "Point of Sale" => "Fast checkout and billing workspace",
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
            "Dashboard" => new DashboardViewModel(_dashboardService, NavigateTo),
            "Products" => new ProductsViewModel(_productService),
            _ => new PlaceholderViewModel(key, GetPlaceholderDescription(key))
        };

        if (CurrentViewModel is DashboardViewModel dashboard)
            _ = dashboard.LoadAsync();
        if (CurrentViewModel is ProductsViewModel products)
            _ = products.LoadAsync();
    }

    private static string GetPlaceholderDescription(string key) => key switch
    {
        "Point of Sale" => "The checkout workspace is reserved for product search, cart management, discounts, split payments, receipt printing and sale returns. This module will be implemented in the next development phase.",
        "Inventory" => "This workspace will manage stock-in and stock-out, stock adjustments, batch numbers, expiry dates, FIFO allocation, damaged goods and stock movement history.",
        "Milk Bar" => "This workspace will manage prepared menu items, recipes and ingredient consumption. Selling a prepared item will deduct its recipe ingredients from stock.",
        "Purchases" => "This workspace will record supplier invoices, purchased quantities, batch details, expiry dates, purchase costs and goods received.",
        "Suppliers" => "This workspace will maintain supplier contact details, purchase history and outstanding supplier balances.",
        "Customers" => "This workspace will maintain optional customer profiles and customer purchase history.",
        "Expenses" => "This workspace will record operating expenses such as utilities, transport, packaging, maintenance and other costs.",
        "Finance" => "This workspace will present revenue, cost of goods sold, gross profit, operating expenses, net profit and cash-flow summaries.",
        "Reports" => "This workspace will produce daily and monthly sales, product, inventory, expiry, purchase, expense and profit-and-loss reports with PDF and Excel export.",
        "Settings" => "This workspace will contain outlet details, receipt settings, database location, backup and restore tools, and application preferences.",
        _ => "This workspace is included in the application plan and will be implemented in a later phase."
    };
}
