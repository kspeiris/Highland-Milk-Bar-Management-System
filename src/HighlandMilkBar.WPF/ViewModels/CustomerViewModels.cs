using System.Collections.ObjectModel;
using System.Globalization;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Customer workspace: KPI cards, filterable/sortable listing and row actions.</summary>
public sealed class CustomersViewModel : ObservableObject
{
    private readonly ICustomerService _customers;
    private readonly ICustomerQueryService _query;
    private readonly ILoyaltyService _loyalty;
    private string _searchText = string.Empty;
    private EnumOption<CustomerType>? _selectedType;
    private EnumOption<bool>? _selectedStatus;
    private EnumOption<CustomerSort>? _selectedSort;
    private CustomerListRowDto? _selectedCustomer;
    private int _page = 1;
    private int _totalPages = 1;
    private int _totalCount;
    private bool _isLoading;
    private string _statusMessage = "Select a customer to view actions.";

    private int _totalCustomers;
    private int _activeCustomers;
    private int _newThisMonth;
    private int _repeatCustomers;
    private decimal _totalCustomerSales;

    public CustomersViewModel(ICustomerService customers, ICustomerQueryService query, ILoyaltyService loyalty)
    {
        _customers = customers;
        _query = query;
        _loyalty = loyalty;

        TypeOptions =
        [
            new EnumOption<CustomerType>("All types", null),
            new EnumOption<CustomerType>("Individual", CustomerType.Individual),
            new EnumOption<CustomerType>("Business", CustomerType.Business)
        ];
        StatusOptions =
        [
            new EnumOption<bool>("All statuses", null),
            new EnumOption<bool>("Active", true),
            new EnumOption<bool>("Inactive", false)
        ];
        SortOptions =
        [
            new EnumOption<CustomerSort>("Name", CustomerSort.Name),
            new EnumOption<CustomerSort>("Spending", CustomerSort.Spending),
            new EnumOption<CustomerSort>("Last purchase", CustomerSort.LastPurchase)
        ];
        SelectedType = TypeOptions[0];
        SelectedStatus = StatusOptions[0];
        SelectedSort = SortOptions[0];

        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        SearchCommand = new RelayCommand(_ => { Page = 1; _ = SearchAsync(); });
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        PreviousPageCommand = new RelayCommand(_ => { if (Page > 1) { Page--; _ = SearchAsync(); } }, _ => Page > 1);
        NextPageCommand = new RelayCommand(_ => { if (Page < TotalPages) { Page++; _ = SearchAsync(); } }, _ => Page < TotalPages);

        NewCommand = new RelayCommand(_ => NewRequested?.Invoke());
        EditCommand = new RelayCommand(_ => Edit(), _ => SelectedCustomer is not null);
        DetailsCommand = new RelayCommand(_ => Details(), _ => SelectedCustomer is not null);
        ToggleActiveCommand = new RelayCommand(_ => _ = ToggleActive(), _ => SelectedCustomer is not null);
        DeleteCommand = new RelayCommand(_ => _ = Delete(), _ => SelectedCustomer is not null);
        AnalyticsCommand = new RelayCommand(_ => AnalyticsRequested?.Invoke());
    }

    public ObservableCollection<CustomerListRowDto> Customers { get; } = new();

    public ICustomerService CustomerService => _customers;
    public ICustomerQueryService QueryService => _query;
    public ILoyaltyService LoyaltyService => _loyalty;

    public ObservableCollection<EnumOption<CustomerType>> TypeOptions { get; }
    public ObservableCollection<EnumOption<bool>> StatusOptions { get; }
    public ObservableCollection<EnumOption<CustomerSort>> SortOptions { get; }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DetailsCommand { get; }
    public RelayCommand ToggleActiveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand AnalyticsCommand { get; }

    public event Action? NewRequested;
    public event Action<CustomerListRowDto>? EditRequested;
    public event Action<CustomerListRowDto>? DetailsRequested;
    public event Action? AnalyticsRequested;

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public EnumOption<CustomerType>? SelectedType { get => _selectedType; set => SetProperty(ref _selectedType, value); }
    public EnumOption<bool>? SelectedStatus { get => _selectedStatus; set => SetProperty(ref _selectedStatus, value); }
    public EnumOption<CustomerSort>? SelectedSort { get => _selectedSort; set => SetProperty(ref _selectedSort, value); }

    public CustomerListRowDto? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                StatusMessage = value is null
                    ? "Select a customer to view actions."
                    : $"{value.Name} · {value.TotalPurchases} purchase(s) · LKR {value.TotalSpending:N2}";
                EditCommand.RaiseCanExecuteChanged();
                DetailsCommand.RaiseCanExecuteChanged();
                ToggleActiveCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int Page { get => _page; private set { if (SetProperty(ref _page, value)) OnPropertyChanged(nameof(PageDisplay)); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { OnPropertyChanged(nameof(PageDisplay)); NextPageCommand.RaiseCanExecuteChanged(); PreviousPageCommand.RaiseCanExecuteChanged(); } } }
    public int TotalCount { get => _totalCount; private set => SetProperty(ref _totalCount, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string PageDisplay => $"Page {Page} of {Math.Max(TotalPages, 1)} · {TotalCount} customer(s)";

    public int TotalCustomers { get => _totalCustomers; private set => SetProperty(ref _totalCustomers, value); }
    public int ActiveCustomers { get => _activeCustomers; private set => SetProperty(ref _activeCustomers, value); }
    public int NewThisMonth { get => _newThisMonth; private set => SetProperty(ref _newThisMonth, value); }
    public int RepeatCustomers { get => _repeatCustomers; private set => SetProperty(ref _repeatCustomers, value); }
    public decimal TotalCustomerSales { get => _totalCustomerSales; private set => SetProperty(ref _totalCustomerSales, value); }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var stats = await _query.GetStatsAsync();
            TotalCustomers = stats.TotalCustomers;
            ActiveCustomers = stats.ActiveCustomers;
            NewThisMonth = stats.NewThisMonth;
            RepeatCustomers = stats.RepeatCustomers;
            TotalCustomerSales = stats.TotalCustomerSales;
            await SearchAsync();
        }
        finally { IsLoading = false; }
    }

    public async Task SearchAsync()
    {
        var result = await _query.SearchAsync(new CustomerSearchFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            Type = SelectedType?.Value,
            IsActive = SelectedStatus?.Value,
            Sort = SelectedSort?.Value ?? CustomerSort.Name,
            Page = Page,
            PageSize = 15
        });

        Customers.Clear();
        foreach (var c in result.Items) Customers.Add(c);
        TotalCount = result.TotalCount;
        TotalPages = result.TotalPages;
        if (Page > TotalPages && TotalPages > 0) { Page = TotalPages; _ = SearchAsync(); }
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedType = TypeOptions[0];
        SelectedStatus = StatusOptions[0];
        SelectedSort = SortOptions[0];
        Page = 1;
        _ = SearchAsync();
    }

    private void Edit() { if (SelectedCustomer is { } c) EditRequested?.Invoke(c); }
    private void Details() { if (SelectedCustomer is { } c) DetailsRequested?.Invoke(c); }

    private async Task ToggleActive()
    {
        if (SelectedCustomer is not { } c) return;
        await _customers.SetActiveAsync(c.Id, !c.IsActive);
        await LoadAsync();
    }

    private async Task Delete()
    {
        if (SelectedCustomer is not { } c) return;
        try
        {
            await _customers.DeleteAsync(c.Id);
            StatusMessage = $"Customer '{c.Name}' deleted.";
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }
}

/// <summary>Customer profile: overview statistics and the history/loyalty tabs.</summary>
public sealed class CustomerDetailsViewModel : ObservableObject
{
    private readonly ICustomerQueryService _query;
    private readonly ILoyaltyService _loyalty;
    private int _customerId;
    private bool _isBusy;
    private string? _error;

    public CustomerDetailsViewModel(ICustomerQueryService query, ILoyaltyService loyalty)
    {
        _query = query;
        _loyalty = loyalty;
    }

    public ObservableCollection<CustomerPurchaseHistoryRowDto> PurchaseHistory { get; } = new();
    public ObservableCollection<CustomerPurchasedProductDto> PurchasedProducts { get; } = new();
    public ObservableCollection<CustomerPaymentDto> Payments { get; } = new();
    public ObservableCollection<CustomerReturnDto> Returns { get; } = new();
    public ObservableCollection<LoyaltyTransactionDto> LoyaltyHistory { get; } = new();

    public string Name { get; private set; } = string.Empty;
    public string CustomerCode { get; private set; } = string.Empty;
    public string TypeText { get; private set; } = string.Empty;
    public string Phone { get; private set; } = "—";
    public string AlternativePhone { get; private set; } = "—";
    public string Email { get; private set; } = "—";
    public string Address { get; private set; } = "—";
    public string City { get; private set; } = "—";
    public string DateOfBirth { get; private set; } = "—";
    public string Notes { get; private set; } = "—";
    public string StatusText { get; private set; } = string.Empty;
    public string RegistrationDate { get; private set; } = "—";
    public bool IsActive { get; private set; }

    public int TotalPurchases { get; private set; }
    public decimal TotalSpending { get; private set; }
    public decimal AveragePurchaseValue { get; private set; }
    public DateTime? LastPurchase { get; private set; }
    public decimal TotalReturns { get; private set; }

    public int PointsBalance { get; private set; }
    public int PointsEarned { get; private set; }
    public int PointsRedeemed { get; private set; }
    public int PointsReversed { get; private set; }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(_error);

    public async Task LoadAsync(int customerId)
    {
        _customerId = customerId;
        IsBusy = true;
        Error = null;
        try
        {
            var profile = await _query.GetProfileAsync(customerId);
            if (profile is null) { Error = "Customer not found."; return; }

            Name = profile.Name;
            CustomerCode = profile.CustomerCode;
            TypeText = profile.TypeText;
            Phone = profile.Phone ?? "—";
            AlternativePhone = profile.AlternativePhone ?? "—";
            Email = profile.Email ?? "—";
            Address = profile.Address ?? "—";
            City = profile.City ?? "—";
            DateOfBirth = profile.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";
            Notes = profile.Notes ?? "—";
            StatusText = profile.StatusText;
            IsActive = profile.IsActive;
            RegistrationDate = profile.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            TotalPurchases = profile.TotalPurchases;
            TotalSpending = profile.TotalSpending;
            AveragePurchaseValue = profile.AveragePurchaseValue;
            LastPurchase = profile.LastPurchase;
            TotalReturns = profile.TotalReturns;
            PointsBalance = profile.PointsBalance;

            var history = await _query.GetPurchaseHistoryAsync(customerId);
            PurchaseHistory.Clear();
            foreach (var h in history) PurchaseHistory.Add(h);

            var products = await _query.GetPurchasedProductsAsync(customerId);
            PurchasedProducts.Clear();
            foreach (var p in products) PurchasedProducts.Add(p);

            var payments = await _query.GetPaymentsAsync(customerId);
            Payments.Clear();
            foreach (var p in payments) Payments.Add(p);

            var returns = await _query.GetReturnsAsync(customerId);
            Returns.Clear();
            foreach (var r in returns) Returns.Add(r);

            var balance = await _loyalty.GetBalanceAsync(customerId);
            PointsBalance = balance.Balance;
            PointsEarned = balance.TotalEarned;
            PointsRedeemed = balance.TotalRedeemed;
            PointsReversed = balance.TotalReversed;

            var ledger = await _loyalty.GetHistoryAsync(customerId);
            LoyaltyHistory.Clear();
            foreach (var l in ledger) LoyaltyHistory.Add(l);

            RaiseAll();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    public Task RefreshAsync() => LoadAsync(_customerId);

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(Name), nameof(CustomerCode), nameof(TypeText), nameof(Phone), nameof(AlternativePhone),
                     nameof(Email), nameof(Address), nameof(City), nameof(DateOfBirth), nameof(Notes), nameof(StatusText),
                     nameof(RegistrationDate), nameof(IsActive), nameof(TotalPurchases), nameof(TotalSpending),
                     nameof(AveragePurchaseValue), nameof(LastPurchase), nameof(TotalReturns), nameof(PointsBalance),
                     nameof(PointsEarned), nameof(PointsRedeemed), nameof(PointsReversed)
                 })
            OnPropertyChanged(name);
    }
}

/// <summary>Customer analytics: KPIs, rankings, segmentation and loyalty configuration.</summary>
public sealed class CustomerAnalyticsViewModel : ObservableObject
{
    private readonly ICustomerQueryService _query;
    private readonly ILoyaltyService _loyalty;
    private bool _isBusy;
    private string? _error;
    private decimal _pointsPerHundred = 1m;
    private decimal _minimumQualifying = 500m;
    private bool _loyaltyEnabled = true;
    private string _statusMessage = string.Empty;

    public CustomerAnalyticsViewModel(ICustomerQueryService query, ILoyaltyService loyalty)
    {
        _query = query;
        _loyalty = loyalty;
    }

    public ObservableCollection<CustomerRankDto> TopBySpending { get; } = new();
    public ObservableCollection<CustomerRankDto> TopByFrequency { get; } = new();
    public ObservableCollection<CustomerRankDto> InactiveCustomers { get; } = new();
    public ObservableCollection<CustomerSegmentCountDto> SegmentCounts { get; } = new();

    public int TotalCustomers { get; private set; }
    public int ActiveCustomers { get; private set; }
    public int NewThisMonth { get; private set; }
    public int RepeatCustomers { get; private set; }
    public decimal TotalCustomerSales { get; private set; }
    public decimal AverageCustomerSpending { get; private set; }

    public decimal PointsPerHundred { get => _pointsPerHundred; set => SetProperty(ref _pointsPerHundred, value); }
    public decimal MinimumQualifying { get => _minimumQualifying; set => SetProperty(ref _minimumQualifying, value); }
    public bool LoyaltyEnabled { get => _loyaltyEnabled; set => SetProperty(ref _loyaltyEnabled, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(_error);

    public async Task LoadAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var data = await _query.GetAnalyticsAsync();
            TotalCustomers = data.TotalCustomers;
            ActiveCustomers = data.ActiveCustomers;
            NewThisMonth = data.NewThisMonth;
            RepeatCustomers = data.RepeatCustomers;
            TotalCustomerSales = data.TotalCustomerSales;
            AverageCustomerSpending = data.AverageCustomerSpending;

            TopBySpending.Clear();
            foreach (var r in data.TopCustomersBySpending) TopBySpending.Add(r);
            TopByFrequency.Clear();
            foreach (var r in data.TopCustomersByFrequency) TopByFrequency.Add(r);
            InactiveCustomers.Clear();
            foreach (var r in data.InactiveCustomers) InactiveCustomers.Add(r);
            SegmentCounts.Clear();
            foreach (var s in data.SegmentCounts) SegmentCounts.Add(s);

            var settings = await _loyalty.GetSettingsAsync();
            PointsPerHundred = settings.PointsPerHundred;
            MinimumQualifying = settings.MinimumQualifyingAmount;
            LoyaltyEnabled = settings.Enabled;

            RaiseAll();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    public async Task SaveLoyaltyAsync()
    {
        try
        {
            await _loyalty.UpdateSettingsAsync(new LoyaltyUpdateSettingsRequest
            {
                PointsPerHundred = PointsPerHundred,
                MinimumQualifyingAmount = MinimumQualifying,
                Enabled = LoyaltyEnabled
            });
            StatusMessage = "Loyalty settings saved.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(TotalCustomers), nameof(ActiveCustomers), nameof(NewThisMonth), nameof(RepeatCustomers),
                     nameof(TotalCustomerSales), nameof(AverageCustomerSpending)
                 })
            OnPropertyChanged(name);
    }
}