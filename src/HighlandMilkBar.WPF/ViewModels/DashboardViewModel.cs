using System.Collections.ObjectModel;
using System.Windows.Input;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private string _revenueTodayText = "LKR 0.00";
    private int _transactionsToday;
    private int _activeProducts;
    private int _lowStockItems;
    private string _lastUpdatedText = "Not refreshed yet";

    public DashboardViewModel(IDashboardService dashboardService, Action<string> navigate)
    {
        _dashboardService = dashboardService;
        NavigateToProductsCommand = new RelayCommand(_ => navigate("Products"));
        NavigateToPosCommand = new RelayCommand(_ => navigate("Point of Sale"));
        NavigateToInventoryCommand = new RelayCommand(_ => navigate("Inventory"));
        NavigateToReportsCommand = new RelayCommand(_ => navigate("Reports"));
        NavigateToSettingsCommand = new RelayCommand(_ => navigate("Settings"));

        LoadDemoData();
    }

    public string Greeting => DateTime.Now.Hour < 12 ? "Good morning" : DateTime.Now.Hour < 17 ? "Good afternoon" : "Good evening";
    public string TodayText => DateTime.Now.ToString("dddd, dd MMMM yyyy");

    // Real values from the database service.
    public string RevenueTodayText { get => _revenueTodayText; private set => SetProperty(ref _revenueTodayText, value); }
    public int TransactionsToday { get => _transactionsToday; private set => SetProperty(ref _transactionsToday, value); }
    public int ActiveProducts { get => _activeProducts; private set => SetProperty(ref _activeProducts, value); }
    public int LowStockItems { get => _lowStockItems; private set => SetProperty(ref _lowStockItems, value); }
    public string LastUpdatedText { get => _lastUpdatedText; private set => SetProperty(ref _lastUpdatedText, value); }

    // Demo values for layout preview only (no profit/expense service exists yet).
    public string ProfitTodayText { get; } = "LKR 18,450.00";
    public string ExpensesTodayText { get; } = "LKR 4,200.00";
    public string DemoNote => "Figures marked DEMO are sample data for layout preview only and are not real financial information.";

    public ObservableCollection<RecentSaleRow> RecentSales { get; } = new();
    public ObservableCollection<CategorySaleRow> CategorySales { get; } = new();
    public ObservableCollection<TopProductRow> TopProducts { get; } = new();
    public ObservableCollection<StockAlertRow> LowStockAlerts { get; } = new();
    public ObservableCollection<StockAlertRow> NearExpiryAlerts { get; } = new();
    public ObservableCollection<StockAlertRow> ExpiredAlerts { get; } = new();

    public ICommand NavigateToProductsCommand { get; }
    public ICommand NavigateToPosCommand { get; }
    public ICommand NavigateToInventoryCommand { get; }
    public ICommand NavigateToReportsCommand { get; }
    public ICommand NavigateToSettingsCommand { get; }

    public async Task LoadAsync()
    {
        var snapshot = await _dashboardService.GetSnapshotAsync();
        RevenueTodayText = $"LKR {snapshot.RevenueToday:N2}";
        TransactionsToday = snapshot.TransactionsToday;
        ActiveProducts = snapshot.ActiveProducts;
        LowStockItems = snapshot.LowStockItems;
        LastUpdatedText = $"Updated {DateTime.Now:hh:mm tt}";
    }

    private void LoadDemoData()
    {
        RecentSales.Add(new RecentSaleRow("INV-1042", "2026-11-14 10:15", 3, "Cash", "LKR 1,250.00", "Completed"));
        RecentSales.Add(new RecentSaleRow("INV-1041", "2026-11-14 09:40", 5, "Card", "LKR 2,480.00", "Completed"));
        RecentSales.Add(new RecentSaleRow("INV-1040", "2026-11-14 09:05", 2, "Cash", "LKR 640.00", "Completed"));
        RecentSales.Add(new RecentSaleRow("INV-1039", "2026-11-13 18:22", 4, "Cash", "LKR 1,920.00", "Returned"));
        RecentSales.Add(new RecentSaleRow("INV-1038", "2026-11-13 17:50", 1, "Card", "LKR 350.00", "Completed"));

        CategorySales.Add(new CategorySaleRow("Fresh Milk", 42, "LKR 52,500"));
        CategorySales.Add(new CategorySaleRow("Ice Cream", 22, "LKR 27,500"));
        CategorySales.Add(new CategorySaleRow("Milk Powder", 18, "LKR 22,500"));
        CategorySales.Add(new CategorySaleRow("Beverages", 12, "LKR 15,000"));
        CategorySales.Add(new CategorySaleRow("Others", 6, "LKR 7,500"));

        TopProducts.Add(new TopProductRow("Highland Fresh Milk 1L", 128, "LKR 25,600"));
        TopProducts.Add(new TopProductRow("Chocolate Milkshake", 96, "LKR 48,000"));
        TopProducts.Add(new TopProductRow("Vanilla Ice Cream Cup", 74, "LKR 22,200"));
        TopProducts.Add(new TopProductRow("Highland Yoghurt", 61, "LKR 12,200"));

        LowStockAlerts.Add(new StockAlertRow("Highland Fresh Milk 1L", "6 bottles left"));
        LowStockAlerts.Add(new StockAlertRow("Highland Milk Powder", "4 packs left"));

        NearExpiryAlerts.Add(new StockAlertRow("Highland Yoghurt", "Expires in 2 days"));
        NearExpiryAlerts.Add(new StockAlertRow("Fresh Milk batch B-1042", "Expires in 3 days"));

        ExpiredAlerts.Add(new StockAlertRow("Curd batch B-2210", "Expired 1 day ago"));
    }
}

public sealed record RecentSaleRow(string Invoice, string Date, int Items, string Payment, string Total, string Status);
public sealed record CategorySaleRow(string Category, int Percent, string Amount);
public sealed record TopProductRow(string Name, int Units, string Amount);
public sealed record StockAlertRow(string Product, string Detail);