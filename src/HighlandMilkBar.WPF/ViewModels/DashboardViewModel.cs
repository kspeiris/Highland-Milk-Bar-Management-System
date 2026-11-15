using System.Collections.ObjectModel;
using System.Windows.Input;
using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Business overview dashboard backed by <see cref="IBusinessAnalyticsQueryService"/> (read-only).</summary>
public sealed class DashboardViewModel : ObservableObject
{
    private readonly IBusinessAnalyticsQueryService _analytics;
    private readonly Action<string> _navigate;

    private EnumOption<ReportingPreset>? _selectedPreset;
    private string _periodLabel = string.Empty;
    private string _lastUpdatedText = "Not refreshed yet";
    private bool _isBusy;
    private string? _error;

    private decimal _netSales;
    private decimal _grossProfit;
    private decimal _operatingProfit;
    private decimal _expenses;
    private int _orders;
    private decimal _averageOrderValue;
    private int _totalCustomers;
    private int _newCustomers;
    private int _repeatCustomers;
    private decimal _physicalStockValue;
    private decimal _sellableStockValue;
    private int _lowStockProducts;
    private int _expiredBatches;
    private decimal _trendMax;

    private string _netSalesChange = string.Empty;
    private string _grossProfitChange = string.Empty;
    private string _expensesChange = string.Empty;
    private string _ordersChange = string.Empty;

    public DashboardViewModel(IBusinessAnalyticsQueryService analytics, Action<string> navigate)
    {
        _analytics = analytics;
        _navigate = navigate;

        PeriodOptions =
        [
            new EnumOption<ReportingPreset>("This month", ReportingPreset.ThisMonth),
            new EnumOption<ReportingPreset>("Last month", ReportingPreset.LastMonth),
            new EnumOption<ReportingPreset>("This week", ReportingPreset.ThisWeek),
            new EnumOption<ReportingPreset>("Last 7 days", ReportingPreset.LastWeek),
            new EnumOption<ReportingPreset>("This year", ReportingPreset.ThisYear),
            new EnumOption<ReportingPreset>("Today", ReportingPreset.Today)
        ];
        SelectedPreset = PeriodOptions[0];

        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        NavigateToProductsCommand = new RelayCommand(_ => _navigate("Products"));
        NavigateToPosCommand = new RelayCommand(_ => _navigate("Point of Sale"));
        NavigateToInventoryCommand = new RelayCommand(_ => _navigate("Inventory"));
        NavigateToAnalyticsCommand = new RelayCommand(_ => _navigate("Analytics"));
        NavigateToReportsCommand = new RelayCommand(_ => _navigate("Reports"));
    }

    public ObservableCollection<EnumOption<ReportingPreset>> PeriodOptions { get; }
    public ObservableCollection<SalesTrendPointDto> Trend { get; } = new();
    public ObservableCollection<CategorySalesDto> CategorySales { get; } = new();
    public ObservableCollection<ProductPerformanceDto> TopProducts { get; } = new();
    public ObservableCollection<BusinessAlertDto> Alerts { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public ICommand NavigateToProductsCommand { get; }
    public ICommand NavigateToPosCommand { get; }
    public ICommand NavigateToInventoryCommand { get; }
    public ICommand NavigateToAnalyticsCommand { get; }
    public ICommand NavigateToReportsCommand { get; }

    public string Greeting => DateTime.Now.Hour < 12 ? "Good morning" : DateTime.Now.Hour < 17 ? "Good afternoon" : "Good evening";
    public string TodayText => DateTime.Now.ToString("dddd, dd MMMM yyyy");

    public EnumOption<ReportingPreset>? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && value?.Value is { } preset)
                _ = LoadAsync(preset);
        }
    }

    public string PeriodLabel { get => _periodLabel; private set => SetProperty(ref _periodLabel, value); }
    public string LastUpdatedText { get => _lastUpdatedText; private set => SetProperty(ref _lastUpdatedText, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(Error);

    public decimal NetSales { get => _netSales; private set { if (SetProperty(ref _netSales, value)) OnPropertyChanged(nameof(NetSalesChange)); } }
    public decimal GrossProfit { get => _grossProfit; private set { if (SetProperty(ref _grossProfit, value)) OnPropertyChanged(nameof(GrossProfitChange)); } }
    public decimal OperatingProfit { get => _operatingProfit; private set => SetProperty(ref _operatingProfit, value); }
    public decimal Expenses { get => _expenses; private set { if (SetProperty(ref _expenses, value)) OnPropertyChanged(nameof(ExpensesChange)); } }
    public int Orders { get => _orders; private set { if (SetProperty(ref _orders, value)) OnPropertyChanged(nameof(OrdersChange)); } }
    public decimal AverageOrderValue { get => _averageOrderValue; private set => SetProperty(ref _averageOrderValue, value); }
    public int TotalCustomers { get => _totalCustomers; private set => SetProperty(ref _totalCustomers, value); }
    public int NewCustomers { get => _newCustomers; private set => SetProperty(ref _newCustomers, value); }
    public int RepeatCustomers { get => _repeatCustomers; private set => SetProperty(ref _repeatCustomers, value); }
    public decimal PhysicalStockValue { get => _physicalStockValue; private set => SetProperty(ref _physicalStockValue, value); }
    public decimal SellableStockValue { get => _sellableStockValue; private set => SetProperty(ref _sellableStockValue, value); }
    public int LowStockProducts { get => _lowStockProducts; private set => SetProperty(ref _lowStockProducts, value); }
    public int ExpiredBatches { get => _expiredBatches; private set => SetProperty(ref _expiredBatches, value); }
    public decimal TrendMax { get => _trendMax; private set => SetProperty(ref _trendMax, value); }

    public string NetSalesChange { get => _netSalesChange; private set => SetProperty(ref _netSalesChange, value); }
    public string GrossProfitChange { get => _grossProfitChange; private set => SetProperty(ref _grossProfitChange, value); }
    public string ExpensesChange { get => _expensesChange; private set => SetProperty(ref _expensesChange, value); }
    public string OrdersChange { get => _ordersChange; private set => SetProperty(ref _ordersChange, value); }

    public async Task LoadAsync() => await LoadAsync(SelectedPreset?.Value ?? ReportingPreset.ThisMonth);

    private async Task LoadAsync(ReportingPreset preset)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        try
        {
            var period = ReportingPeriod.FromPreset(preset);
            var data = await _analytics.GetOverviewAsync(period);

            PeriodLabel = data.PeriodLabel;
            NetSales = data.Financial.NetSales;
            GrossProfit = data.Financial.GrossProfit;
            OperatingProfit = data.Financial.OperatingProfit;
            Expenses = data.Financial.OperatingExpenses;
            Orders = data.Orders;
            AverageOrderValue = data.AverageOrderValue;
            TotalCustomers = data.TotalCustomers;
            NewCustomers = data.NewCustomers;
            RepeatCustomers = data.RepeatCustomers;
            PhysicalStockValue = data.Inventory.PhysicalStockValue;
            SellableStockValue = data.Inventory.SellableStockValue;
            LowStockProducts = data.Inventory.LowStockProducts;
            ExpiredBatches = data.Inventory.ExpiredBatches;

            NetSalesChange = data.NetSalesComparison.ChangeText;
            GrossProfitChange = data.GrossProfitComparison.ChangeText;
            ExpensesChange = data.ExpensesComparison.ChangeText;
            OrdersChange = data.OrdersComparison.ChangeText;

            Replace(Trend, data.Trend);
            Replace(CategorySales, data.CategorySales);
            Replace(TopProducts, data.TopProducts);
            Replace(Alerts, data.Alerts);

            TrendMax = data.Trend.Count == 0 ? 1m : Math.Max(1m, data.Trend.Max(t => t.NetSales));
            LastUpdatedText = $"Updated {DateTime.Now:hh:mm tt}";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}