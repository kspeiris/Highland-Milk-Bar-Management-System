using System.Collections.ObjectModel;
using System.Globalization;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>A single row in a rendered P&amp;L / cash-flow table.</summary>
public sealed class FinancialLine
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public bool IsTotal { get; init; }
    public bool IsNegative { get; init; }

    public string AmountText => Amount.ToString("N2", CultureInfo.CurrentCulture);
}

/// <summary>Financial workspace: KPI cards plus P&amp;L, cash flow, reconciliation, payables and payment analysis.</summary>
public sealed class FinancialViewModel : ObservableObject
{
    private readonly IFinancialQueryService _query;

    private PeriodOption? _selectedPeriod;
    private string _fromDate = string.Empty;
    private string _toDate = string.Empty;
    private bool _isBusy;
    private string _statusMessage = "Choose a period and select Apply.";
    private string _periodLabel = string.Empty;

    private decimal _grossSales;
    private decimal _salesDiscounts;
    private decimal _salesReturns;
    private decimal _netSales;
    private decimal _costOfGoodsSold;
    private decimal _grossProfit;
    private decimal _operatingExpenses;
    private decimal _operatingProfit;
    private decimal _grossMargin;
    private decimal _netMargin;

    private decimal _cashInflows;
    private decimal _cashOutflows;
    private decimal _operatingCashFlow;

    private decimal _supplierOutstanding;
    private decimal _supplierOverdue;
    private int _supplierCount;

    private decimal _previousNetSales;
    private decimal _previousOperatingProfit;

    public FinancialViewModel(IFinancialQueryService query)
    {
        _query = query;

        PeriodOptions =
        [
            new PeriodOption("This month", FinancialPreset.ThisMonth),
            new PeriodOption("Last month", FinancialPreset.LastMonth),
            new PeriodOption("Last 30 days", FinancialPreset.Last30Days),
            new PeriodOption("This year", FinancialPreset.ThisYear),
            new PeriodOption("Today", FinancialPreset.Today)
        ];
        SelectedPeriod = PeriodOptions[0];

        ApplyCommand = new RelayCommand(_ => _ = LoadAsync());
        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
    }

    public ObservableCollection<PeriodOption> PeriodOptions { get; }
    public ObservableCollection<FinancialLine> PnlLines { get; } = new();
    public ObservableCollection<FinancialLine> CashInflowLines { get; } = new();
    public ObservableCollection<FinancialLine> CashOutflowLines { get; } = new();
    public ObservableCollection<PaymentBreakdownDto> SalesPayments { get; } = new();
    public ObservableCollection<PaymentBreakdownDto> SupplierPayments { get; } = new();
    public ObservableCollection<PaymentBreakdownDto> ExpensePayments { get; } = new();
    public ObservableCollection<SupplierLiabilityLineDto> SupplierLines { get; } = new();
    public ObservableCollection<SupplierAgeingBucketDto> AgeingBuckets { get; } = new();
    public ObservableCollection<CashReconciliationDto> ReconciliationRows { get; } = new();
    public ObservableCollection<FinancialSeriesPointDto> MonthlySeries { get; } = new();

    public RelayCommand ApplyCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public PeriodOption? SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value) && value is not null)
            {
                var (from, to) = ResolveRange(value.Preset);
                FromDate = from.ToString("yyyy-MM-dd");
                ToDate = to.AddDays(-1).ToString("yyyy-MM-dd");
            }
        }
    }

    public string FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public string ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string PeriodLabel { get => _periodLabel; private set => SetProperty(ref _periodLabel, value); }

    public decimal GrossSales { get => _grossSales; private set => SetProperty(ref _grossSales, value); }
    public decimal SalesDiscounts { get => _salesDiscounts; private set => SetProperty(ref _salesDiscounts, value); }
    public decimal SalesReturns { get => _salesReturns; private set => SetProperty(ref _salesReturns, value); }
    public decimal NetSales { get => _netSales; private set { if (SetProperty(ref _netSales, value)) OnPropertyChanged(nameof(NetSalesChangeText)); } }
    public decimal CostOfGoodsSold { get => _costOfGoodsSold; private set => SetProperty(ref _costOfGoodsSold, value); }
    public decimal GrossProfit { get => _grossProfit; private set { if (SetProperty(ref _grossProfit, value)) OnPropertyChanged(nameof(GrossMarginText)); } }
    public decimal OperatingExpenses { get => _operatingExpenses; private set => SetProperty(ref _operatingExpenses, value); }
    public decimal OperatingProfit { get => _operatingProfit; private set { if (SetProperty(ref _operatingProfit, value)) { OnPropertyChanged(nameof(NetMarginText)); OnPropertyChanged(nameof(OperatingProfitChangeText)); } } }
    public decimal GrossMargin { get => _grossMargin; private set => SetProperty(ref _grossMargin, value); }
    public decimal NetMargin { get => _netMargin; private set => SetProperty(ref _netMargin, value); }

    public decimal CashInflows { get => _cashInflows; private set => SetProperty(ref _cashInflows, value); }
    public decimal CashOutflows { get => _cashOutflows; private set => SetProperty(ref _cashOutflows, value); }
    public decimal OperatingCashFlow { get => _operatingCashFlow; private set => SetProperty(ref _operatingCashFlow, value); }

    public decimal SupplierOutstanding { get => _supplierOutstanding; private set => SetProperty(ref _supplierOutstanding, value); }
    public decimal SupplierOverdue { get => _supplierOverdue; private set => SetProperty(ref _supplierOverdue, value); }
    public int SupplierCount { get => _supplierCount; private set => SetProperty(ref _supplierCount, value); }

    public decimal PreviousNetSales { get => _previousNetSales; private set { if (SetProperty(ref _previousNetSales, value)) OnPropertyChanged(nameof(NetSalesChangeText)); } }
    public decimal PreviousOperatingProfit { get => _previousOperatingProfit; private set { if (SetProperty(ref _previousOperatingProfit, value)) OnPropertyChanged(nameof(OperatingProfitChangeText)); } }

    public string GrossMarginText => $"{GrossMargin:N1}% margin";
    public string NetMarginText => $"{NetMargin:N1}% margin";
    public string NetSalesChangeText => ChangeText(NetSales, PreviousNetSales);
    public string OperatingProfitChangeText => ChangeText(OperatingProfit, PreviousOperatingProfit);

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Loading financials…";
        try
        {
            var from = ParseDate(FromDate) ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var toExclusive = (ParseDate(ToDate) ?? DateTime.Now).AddDays(1);
            if (toExclusive <= from) toExclusive = from.AddDays(1);

            var length = toExclusive - from;
            var prevTo = from;
            var prevFrom = from - length;

            var summary = await _query.GetSummaryAsync(from, toExclusive);
            var previous = await _query.GetSummaryAsync(prevFrom, prevTo);

            GrossSales = summary.GrossSales;
            SalesDiscounts = summary.SalesDiscounts;
            SalesReturns = summary.SalesReturns;
            NetSales = summary.NetSales;
            CostOfGoodsSold = summary.CostOfGoodsSold;
            GrossProfit = summary.GrossProfit;
            OperatingExpenses = summary.OperatingExpenses;
            OperatingProfit = summary.OperatingProfit;
            GrossMargin = summary.GrossMarginPercent;
            NetMargin = summary.NetMarginPercent;

            CashInflows = summary.CashInflows;
            CashOutflows = summary.CashOutflows;
            OperatingCashFlow = summary.OperatingCashFlow;

            SupplierOutstanding = summary.SupplierOutstanding;

            PreviousNetSales = previous.NetSales;
            PreviousOperatingProfit = previous.OperatingProfit;

            PeriodLabel = $"{from:dd MMM yyyy} – {toExclusive.AddDays(-1):dd MMM yyyy}";

            BuildPnl(summary);
            await LoadCashFlowAsync(from, toExclusive);
            await LoadPaymentsAsync(from, toExclusive);
            await LoadLiabilitiesAsync();
            await LoadReconciliationAsync();
            await LoadSeriesAsync();

            StatusMessage = $"Loaded financials for {PeriodLabel}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildPnl(FinancialSummaryDto s)
    {
        PnlLines.Clear();
        PnlLines.Add(new FinancialLine { Label = "Gross sales", Amount = s.GrossSales });
        PnlLines.Add(new FinancialLine { Label = "Less: discounts", Amount = s.SalesDiscounts, IsNegative = true });
        PnlLines.Add(new FinancialLine { Label = "Less: sales returns", Amount = s.SalesReturns, IsNegative = true });
        PnlLines.Add(new FinancialLine { Label = "Net sales", Amount = s.NetSales, IsTotal = true });
        PnlLines.Add(new FinancialLine { Label = "Cost of goods sold", Amount = s.CostOfGoodsSold, IsNegative = true });
        PnlLines.Add(new FinancialLine { Label = "Gross profit", Amount = s.GrossProfit, IsTotal = true });
        PnlLines.Add(new FinancialLine { Label = "Operating expenses", Amount = s.OperatingExpenses, IsNegative = true });
        PnlLines.Add(new FinancialLine { Label = "Operating profit", Amount = s.OperatingProfit, IsTotal = true });
    }

    private async Task LoadCashFlowAsync(DateTime from, DateTime to)
    {
        var flow = await _query.GetCashFlowAsync(from, to);
        CashInflowLines.Clear();
        foreach (var line in flow.Inflows) CashInflowLines.Add(new FinancialLine { Label = WithCount(line), Amount = line.Amount });
        CashInflowLines.Add(new FinancialLine { Label = "Total cash in", Amount = flow.CashInflows, IsTotal = true });

        CashOutflowLines.Clear();
        foreach (var line in flow.Outflows) CashOutflowLines.Add(new FinancialLine { Label = WithCount(line), Amount = line.Amount });
        CashOutflowLines.Add(new FinancialLine { Label = "Total cash out", Amount = flow.CashOutflows, IsTotal = true });

        OperatingCashFlow = flow.NetCashFlow;
    }

    private async Task LoadPaymentsAsync(DateTime from, DateTime to)
    {
        Replace(SalesPayments, await _query.GetSalesPaymentBreakdownAsync(from, to));
        Replace(SupplierPayments, await _query.GetSupplierPaymentBreakdownAsync(from, to));
        Replace(ExpensePayments, await _query.GetExpensePaymentBreakdownAsync(from, to));
    }

    private async Task LoadLiabilitiesAsync()
    {
        var liabilities = await _query.GetSupplierLiabilitiesAsync();
        SupplierOutstanding = liabilities.TotalOutstanding;
        SupplierOverdue = liabilities.OverdueTotal;
        SupplierCount = liabilities.SupplierCount;
        Replace(SupplierLines, liabilities.Lines);
        Replace(AgeingBuckets, liabilities.AgeingBuckets);
    }

    private async Task LoadReconciliationAsync()
    {
        Replace(ReconciliationRows, await _query.GetCashReconciliationAsync());
    }

    private async Task LoadSeriesAsync()
    {
        Replace(MonthlySeries, await _query.GetMonthlySeriesAsync(12));
    }

    private static (DateTime From, DateTime To) ResolveRange(FinancialPreset preset)
    {
        var now = DateTime.Now;
        var today = now.Date;
        return preset switch
        {
            FinancialPreset.Today => (today, today.AddDays(1)),
            FinancialPreset.ThisMonth => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1)),
            FinancialPreset.LastMonth => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1)),
            FinancialPreset.Last30Days => (today.AddDays(-29), today.AddDays(1)),
            FinancialPreset.ThisYear => (new DateTime(now.Year, 1, 1), new DateTime(now.Year + 1, 1, 1)),
            _ => (new DateTime(now.Year, now.Month, 1), today.AddDays(1))
        };
    }

    private static string WithCount(CashFlowLineDto line) =>
        line.Count > 0 ? $"{line.Label} ({line.Count})" : line.Label;

    private static string ChangeText(decimal current, decimal previous)
    {
        if (previous == 0m)
            return current == 0m ? "no change vs previous period" : "no prior-period baseline";
        var pct = (current - previous) / Math.Abs(previous) * 100m;
        return $"{(pct >= 0 ? "+" : "")}{pct:N1}% vs previous period";
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    private static DateTime? ParseDate(string? text)
        => DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d) ? d.Date : null;
}

/// <summary>A selectable reporting period.</summary>
public sealed class PeriodOption
{
    public PeriodOption(string label, FinancialPreset preset)
    {
        Label = label;
        Preset = preset;
    }

    public string Label { get; }
    public FinancialPreset Preset { get; }
}

public enum FinancialPreset
{
    ThisMonth,
    LastMonth,
    Last30Days,
    ThisYear,
    Today
}