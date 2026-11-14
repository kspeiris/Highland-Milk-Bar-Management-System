using System.Windows.Input;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly Action<string> _navigate;
    private string _revenueTodayText = "LKR 0.00";
    private int _transactionsToday;
    private int _activeProducts;
    private int _lowStockItems;
    private string _lastUpdatedText = "Not refreshed yet";

    public DashboardViewModel(IDashboardService dashboardService, Action<string> navigate)
    {
        _dashboardService = dashboardService;
        _navigate = navigate;
        NavigateToProductsCommand = new RelayCommand(_ => _navigate("Products"));
        NavigateToPosCommand = new RelayCommand(_ => _navigate("Point of Sale"));
        NavigateToInventoryCommand = new RelayCommand(_ => _navigate("Inventory"));
        NavigateToReportsCommand = new RelayCommand(_ => _navigate("Reports"));
    }

    public string Greeting => DateTime.Now.Hour < 12 ? "Good morning" : DateTime.Now.Hour < 17 ? "Good afternoon" : "Good evening";
    public string TodayText => DateTime.Now.ToString("dddd, dd MMMM yyyy");
    public string RevenueTodayText { get => _revenueTodayText; private set => SetProperty(ref _revenueTodayText, value); }
    public int TransactionsToday { get => _transactionsToday; private set => SetProperty(ref _transactionsToday, value); }
    public int ActiveProducts { get => _activeProducts; private set => SetProperty(ref _activeProducts, value); }
    public int LowStockItems { get => _lowStockItems; private set => SetProperty(ref _lowStockItems, value); }
    public string LastUpdatedText { get => _lastUpdatedText; private set => SetProperty(ref _lastUpdatedText, value); }
    public ICommand NavigateToProductsCommand { get; }
    public ICommand NavigateToPosCommand { get; }
    public ICommand NavigateToInventoryCommand { get; }
    public ICommand NavigateToReportsCommand { get; }

    public async Task LoadAsync()
    {
        var snapshot = await _dashboardService.GetSnapshotAsync();
        RevenueTodayText = $"LKR {snapshot.RevenueToday:N2}";
        TransactionsToday = snapshot.TransactionsToday;
        ActiveProducts = snapshot.ActiveProducts;
        LowStockItems = snapshot.LowStockItems;
        LastUpdatedText = $"Updated {DateTime.Now:hh:mm tt}";
    }
}
