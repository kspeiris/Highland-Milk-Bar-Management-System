using System.Collections.ObjectModel;
using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Tabbed business analytics workspace (Sales, Products, Inventory, Customers, Milk Bar, Purchases, Expenses).</summary>
public sealed class AnalyticsViewModel : ObservableObject
{
    private readonly IBusinessAnalyticsQueryService _analytics;
    private EnumOption<ReportingPreset>? _selectedPreset;
    private string _periodLabel = string.Empty;
    private bool _isBusy;
    private string? _error;

    public AnalyticsViewModel(IBusinessAnalyticsQueryService analytics)
    {
        _analytics = analytics;
        PeriodOptions =
        [
            new EnumOption<ReportingPreset>("This month", ReportingPreset.ThisMonth),
            new EnumOption<ReportingPreset>("Last month", ReportingPreset.LastMonth),
            new EnumOption<ReportingPreset>("This week", ReportingPreset.ThisWeek),
            new EnumOption<ReportingPreset>("This year", ReportingPreset.ThisYear),
            new EnumOption<ReportingPreset>("Today", ReportingPreset.Today)
        ];
        SelectedPreset = PeriodOptions[0];
        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
    }

    public ObservableCollection<EnumOption<ReportingPreset>> PeriodOptions { get; }
    public RelayCommand RefreshCommand { get; }

    public SalesAnalyticsDto? Sales { get; private set; }
    public ProductAnalyticsDto? Products { get; private set; }
    public InventoryOverviewDto? Inventory { get; private set; }
    public CustomerAnalyticsDto? Customers { get; private set; }
    public MilkBarOverviewDto? MilkBar { get; private set; }
    public PurchaseDashboardDto? Purchases { get; private set; }
    public ExpenseReportDto? Expenses { get; private set; }

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
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(Error);

    public async Task LoadAsync() => await LoadAsync(SelectedPreset?.Value ?? ReportingPreset.ThisMonth);

    private async Task LoadAsync(ReportingPreset preset)
    {
        if (IsBusy) return;
        IsBusy = true;
        var errors = new List<string>();
        try
        {
            var period = ReportingPeriod.FromPreset(preset);
            PeriodLabel = period.Label;

            Sales = await Guard("Sales", errors, () => _analytics.GetSalesAnalyticsAsync(period));
            Products = await Guard("Products", errors, () => _analytics.GetProductAnalyticsAsync(period));
            Inventory = await Guard("Inventory", errors, () => _analytics.GetInventoryAnalyticsAsync());
            Customers = await Guard("Customers", errors, () => _analytics.GetCustomerAnalyticsAsync());
            MilkBar = await Guard("Milk Bar", errors, () => _analytics.GetMilkBarAnalyticsAsync());
            Purchases = await Guard("Purchases", errors, () => _analytics.GetPurchaseAnalyticsAsync());
            Expenses = await Guard("Expenses", errors, () => _analytics.GetExpenseAnalyticsAsync(period));

            Error = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task<T?> Guard<T>(string area, List<string> errors, Func<Task<T>> load)
    {
        try { return await load(); }
        catch (Exception ex) { errors.Add($"{area}: {ex.Message}"); return default; }
    }
}