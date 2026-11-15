using System.Collections.ObjectModel;
using System.Globalization;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Expense workspace: KPI cards, filters, sortable/paged listing and actions.</summary>
public sealed class ExpensesViewModel : ObservableObject
{
    private readonly IExpenseService _expenses;
    private readonly IExpenseQueryService _query;
    private string _searchText = string.Empty;
    private EnumOption<int>? _selectedCategory;
    private EnumOption<PaymentMethod>? _selectedMethod;
    private EnumOption<ExpenseSort>? _selectedSort;
    private string _fromDate = string.Empty;
    private string _toDate = string.Empty;
    private ExpenseRowDto? _selectedExpense;
    private int _page = 1;
    private int _totalPages = 1;
    private int _totalCount;
    private bool _isLoading;
    private string _statusMessage = "Select an expense to view actions.";

    private decimal _totalExpenses;
    private decimal _monthExpenses;
    private decimal _todayExpenses;
    private int _thisMonthCount;
    private int _categoryCount;

    public ExpensesViewModel(IExpenseService expenses, IExpenseQueryService query)
    {
        _expenses = expenses;
        _query = query;

        MethodOptions =
        [
            new EnumOption<PaymentMethod>("All methods", null),
            new EnumOption<PaymentMethod>("Cash", PaymentMethod.Cash),
            new EnumOption<PaymentMethod>("Card", PaymentMethod.Card),
            new EnumOption<PaymentMethod>("Bank transfer", PaymentMethod.BankTransfer),
            new EnumOption<PaymentMethod>("Cheque", PaymentMethod.Cheque),
            new EnumOption<PaymentMethod>("Other", PaymentMethod.Other)
        ];
        SortOptions =
        [
            new EnumOption<ExpenseSort>("Date", ExpenseSort.Date),
            new EnumOption<ExpenseSort>("Amount", ExpenseSort.Amount),
            new EnumOption<ExpenseSort>("Category", ExpenseSort.Category)
        ];
        SelectedMethod = MethodOptions[0];
        SelectedSort = SortOptions[0];

        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        SearchCommand = new RelayCommand(_ => { Page = 1; _ = SearchAsync(); });
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        PreviousPageCommand = new RelayCommand(_ => { if (Page > 1) { Page--; _ = SearchAsync(); } }, _ => Page > 1);
        NextPageCommand = new RelayCommand(_ => { if (Page < TotalPages) { Page++; _ = SearchAsync(); } }, _ => Page < TotalPages);
        NewCommand = new RelayCommand(_ => NewRequested?.Invoke());
        EditCommand = new RelayCommand(_ => Edit(), _ => SelectedExpense is not null);
        DetailsCommand = new RelayCommand(_ => Details(), _ => SelectedExpense is not null);
        DeleteCommand = new RelayCommand(_ => _ = Delete(), _ => SelectedExpense is not null);
        AnalyticsCommand = new RelayCommand(_ => AnalyticsRequested?.Invoke());
        CategoriesCommand = new RelayCommand(_ => CategoriesRequested?.Invoke());
    }

    public ObservableCollection<ExpenseRowDto> Expenses { get; } = new();
    public ObservableCollection<ExpenseCategoryDto> Categories { get; } = new();
    public ObservableCollection<EnumOption<int>> CategoryOptions { get; } = new();
    public ObservableCollection<EnumOption<PaymentMethod>> MethodOptions { get; }
    public ObservableCollection<EnumOption<ExpenseSort>> SortOptions { get; }

    public IExpenseService ExpenseService => _expenses;
    public IExpenseQueryService QueryService => _query;

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DetailsCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand AnalyticsCommand { get; }
    public RelayCommand CategoriesCommand { get; }

    public event Action? NewRequested;
    public event Action<ExpenseRowDto>? EditRequested;
    public event Action<ExpenseRowDto>? DetailsRequested;
    public event Action? AnalyticsRequested;
    public event Action? CategoriesRequested;

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public string FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public string ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public EnumOption<int>? SelectedCategory { get => _selectedCategory; set => SetProperty(ref _selectedCategory, value); }
    public EnumOption<PaymentMethod>? SelectedMethod { get => _selectedMethod; set => SetProperty(ref _selectedMethod, value); }
    public EnumOption<ExpenseSort>? SelectedSort { get => _selectedSort; set => SetProperty(ref _selectedSort, value); }

    public ExpenseRowDto? SelectedExpense
    {
        get => _selectedExpense;
        set
        {
            if (SetProperty(ref _selectedExpense, value))
            {
                StatusMessage = value is null
                    ? "Select an expense to view actions."
                    : $"{value.Description} · LKR {value.Amount:N2} · {value.ExpenseDate:yyyy-MM-dd}";
                EditCommand.RaiseCanExecuteChanged();
                DetailsCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int Page { get => _page; private set { if (SetProperty(ref _page, value)) OnPropertyChanged(nameof(PageDisplay)); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { OnPropertyChanged(nameof(PageDisplay)); NextPageCommand.RaiseCanExecuteChanged(); PreviousPageCommand.RaiseCanExecuteChanged(); } } }
    public int TotalCount { get => _totalCount; private set => SetProperty(ref _totalCount, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string PageDisplay => $"Page {Page} of {Math.Max(TotalPages, 1)} · {TotalCount} expense(s)";

    public decimal TotalExpenses { get => _totalExpenses; private set => SetProperty(ref _totalExpenses, value); }
    public decimal MonthExpenses { get => _monthExpenses; private set => SetProperty(ref _monthExpenses, value); }
    public decimal TodayExpenses { get => _todayExpenses; private set => SetProperty(ref _todayExpenses, value); }
    public int ThisMonthCount { get => _thisMonthCount; private set => SetProperty(ref _thisMonthCount, value); }
    public int CategoryCount { get => _categoryCount; private set => SetProperty(ref _categoryCount, value); }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            await LoadCategoriesAsync();
            var stats = await _query.GetStatsAsync();
            TotalExpenses = stats.TotalExpenses;
            MonthExpenses = stats.MonthExpenses;
            TodayExpenses = stats.TodayExpenses;
            ThisMonthCount = stats.ThisMonthCount;
            CategoryCount = stats.CategoryCount;
            await SearchAsync();
        }
        finally { IsLoading = false; }
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _expenses.GetCategoriesAsync();
        Categories.Clear();
        foreach (var c in categories) Categories.Add(c);

        CategoryOptions.Clear();
        CategoryOptions.Add(new EnumOption<int>("All categories", null));
        foreach (var c in categories) CategoryOptions.Add(new EnumOption<int>(c.Name, c.Id));
        if (SelectedCategory is null) SelectedCategory = CategoryOptions[0];
    }

    public async Task SearchAsync()
    {
        var result = await _query.SearchAsync(new ExpenseSearchFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            CategoryId = SelectedCategory?.Value,
            PaymentMethod = SelectedMethod?.Value,
            From = ParseDate(FromDate),
            To = ParseDate(ToDate),
            Sort = SelectedSort?.Value ?? ExpenseSort.Date,
            Page = Page,
            PageSize = 15
        });

        Expenses.Clear();
        foreach (var e in result.Items) Expenses.Add(e);
        TotalCount = result.TotalCount;
        TotalPages = result.TotalPages;
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        FromDate = string.Empty;
        ToDate = string.Empty;
        SelectedCategory = CategoryOptions.Count > 0 ? CategoryOptions[0] : null;
        SelectedMethod = MethodOptions[0];
        SelectedSort = SortOptions[0];
        Page = 1;
        _ = SearchAsync();
    }

    private void Edit() { if (SelectedExpense is { } e) EditRequested?.Invoke(e); }
    private void Details() { if (SelectedExpense is { } e) DetailsRequested?.Invoke(e); }

    private async Task Delete()
    {
        if (SelectedExpense is not { } e) return;
        try
        {
            await _expenses.DeleteAsync(e.Id);
            StatusMessage = $"Expense '{e.Description}' deleted.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private static DateTime? ParseDate(string? text)
        => DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d) ? d.Date : null;
}

/// <summary>Expense analytics: monthly trend, category breakdown and range report.</summary>
public sealed class ExpenseAnalyticsViewModel : ObservableObject
{
    private readonly IExpenseQueryService _query;
    private bool _isBusy;
    private string? _error;
    private string _fromDate = string.Empty;
    private string _toDate = string.Empty;
    private decimal _reportTotal;
    private int _reportCount;
    private decimal _reportAverage;

    public ExpenseAnalyticsViewModel(IExpenseQueryService query)
    {
        _query = query;
        var now = DateTime.Now;
        FromDate = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd");
        ToDate = now.ToString("yyyy-MM-dd");
    }

    public ObservableCollection<MonthlyExpenseDto> MonthlyTrend { get; } = new();
    public ObservableCollection<ExpenseCategoryBreakdownDto> CategoryBreakdown { get; } = new();

    public string FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public string ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public decimal ReportTotal { get => _reportTotal; private set => SetProperty(ref _reportTotal, value); }
    public int ReportCount { get => _reportCount; private set => SetProperty(ref _reportCount, value); }
    public decimal ReportAverage { get => _reportAverage; private set => SetProperty(ref _reportAverage, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(_error);

    public async Task LoadAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var trend = await _query.GetMonthlyTrendAsync(6);
            MonthlyTrend.Clear();
            foreach (var t in trend) MonthlyTrend.Add(t);

            var from = DateTime.TryParse(FromDate, out var f) ? f.Date : DateTime.MinValue;
            var to = DateTime.TryParse(ToDate, out var t2) ? t2.Date : DateTime.MaxValue;

            var breakdown = await _query.GetCategoryBreakdownAsync(from, to);
            CategoryBreakdown.Clear();
            foreach (var b in breakdown) CategoryBreakdown.Add(b);

            var report = await _query.GetReportAsync(from, to);
            ReportTotal = report.TotalAmount;
            ReportCount = report.ExpenseCount;
            ReportAverage = report.AverageAmount;
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}