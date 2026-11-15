using System.Collections.ObjectModel;
using System.Windows.Input;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Supplier directory: search, filter, add/edit/deactivate and open details/statement.</summary>
public sealed class SuppliersViewModel : ObservableObject
{
    private readonly ISupplierService _suppliers;
    private string _searchText = string.Empty;
    private EnumOption<bool>? _selectedStatus;
    private SupplierRowDto? _selectedSupplier;
    private int _totalCount;
    private bool _isLoading;
    private string _statusMessage = "Select a supplier to view actions.";

    public SuppliersViewModel(
        ISupplierService suppliers, ISupplierPaymentService payments, IPurchaseService purchases)
    {
        _suppliers = suppliers;
        PaymentService = payments;
        PurchaseService = purchases;
        StatusOptions =
        [
            new EnumOption<bool>("All statuses", null),
            new EnumOption<bool>("Active", true),
            new EnumOption<bool>("Inactive", false)
        ];
        SelectedStatus = StatusOptions[0];

        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        SearchCommand = new RelayCommand(_ => _ = SearchAsync());
        ClearCommand = new RelayCommand(_ => Clear());
        NewCommand = new RelayCommand(_ => NewRequested?.Invoke());
        EditCommand = new RelayCommand(_ => Edit(), _ => SelectedSupplier is not null);
        DetailsCommand = new RelayCommand(_ => Details(), _ => SelectedSupplier is not null);
        StatementCommand = new RelayCommand(_ => Statement(), _ => SelectedSupplier is not null);
        ToggleActiveCommand = new RelayCommand(_ => _ = ToggleActive(), _ => SelectedSupplier is not null);
    }

    public ObservableCollection<SupplierRowDto> Suppliers { get; } = new();
    public ObservableCollection<EnumOption<bool>> StatusOptions { get; }

    public ISupplierService SupplierService => _suppliers;
    public ISupplierPaymentService PaymentService { get; }
    public IPurchaseService PurchaseService { get; }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DetailsCommand { get; }
    public RelayCommand StatementCommand { get; }
    public RelayCommand ToggleActiveCommand { get; }

    public event Action? NewRequested;
    public event Action<SupplierRowDto>? EditRequested;
    public event Action<SupplierRowDto>? DetailsRequested;
    public event Action<SupplierRowDto>? StatementRequested;

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public EnumOption<bool>? SelectedStatus { get => _selectedStatus; set { if (SetProperty(ref _selectedStatus, value)) _ = SearchAsync(); } }

    public SupplierRowDto? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (SetProperty(ref _selectedSupplier, value))
            {
                StatusMessage = value is null
                    ? "Select a supplier to view actions."
                    : $"{value.Name} · Outstanding LKR {value.Outstanding:N2}";
                EditCommand.RaiseCanExecuteChanged();
                DetailsCommand.RaiseCanExecuteChanged();
                StatementCommand.RaiseCanExecuteChanged();
                ToggleActiveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int TotalCount { get => _totalCount; private set => SetProperty(ref _totalCount, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try { await SearchAsync(); }
        finally { IsLoading = false; }
    }

    public async Task SearchAsync()
    {
        var result = await _suppliers.SearchAsync(new SupplierFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            IsActive = SelectedStatus?.Value,
            PageSize = 500
        });
        Suppliers.Clear();
        foreach (var s in result.Items) Suppliers.Add(s);
        TotalCount = result.TotalCount;
    }

    private void Clear()
    {
        SearchText = string.Empty;
        SelectedStatus = StatusOptions[0];
        _ = SearchAsync();
    }

    private void Edit() { if (SelectedSupplier is { } s) EditRequested?.Invoke(s); }
    private void Details() { if (SelectedSupplier is { } s) DetailsRequested?.Invoke(s); }
    private void Statement() { if (SelectedSupplier is { } s) StatementRequested?.Invoke(s); }

    private async Task ToggleActive()
    {
        if (SelectedSupplier is not { } s) return;
        await _suppliers.SetActiveAsync(s.Id, !s.IsActive);
        await SearchAsync();
    }

    public Task<SupplierDetailDto> CreateAsync(CreateSupplierRequest request) => SaveAndReload(() => _suppliers.CreateAsync(request));
    public Task<SupplierDetailDto> UpdateAsync(UpdateSupplierRequest request) => SaveAndReload(() => _suppliers.UpdateAsync(request));

    private async Task<SupplierDetailDto> SaveAndReload(Func<Task<SupplierDetailDto>> action)
    {
        var saved = await action();
        await SearchAsync();
        return saved;
    }
}

/// <summary>Supplier details: overview, purchase history, payment history and a running-balance statement.</summary>
public sealed class SupplierDetailsViewModel : ObservableObject
{
    private readonly ISupplierService _suppliers;
    private readonly ISupplierPaymentService _payments;
    private readonly IPurchaseService _purchases;
    private int _supplierId;
    private bool _isBusy;
    private string? _error;

    public SupplierDetailsViewModel(ISupplierService suppliers, ISupplierPaymentService payments, IPurchaseService purchases)
    {
        _suppliers = suppliers;
        _payments = payments;
        _purchases = purchases;
    }

    public ObservableCollection<SupplierPurchaseDto> Purchases { get; } = new();
    public ObservableCollection<SupplierPaymentDto> Payments { get; } = new();
    public ObservableCollection<StatementLineDto> Statement { get; } = new();

    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string Contact { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string StatusText { get; private set; } = string.Empty;
    public decimal TotalPurchased { get; private set; }
    public decimal TotalPaid { get; private set; }
    public decimal Outstanding { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public decimal ClosingBalance { get; private set; }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(_error);

    public async Task LoadAsync(int supplierId)
    {
        _supplierId = supplierId;
        IsBusy = true;
        Error = null;
        try
        {
            var detail = await _suppliers.GetByIdAsync(supplierId);
            if (detail is null) { Error = "Supplier not found."; return; }

            Name = detail.Name;
            Code = string.IsNullOrWhiteSpace(detail.Code) ? "—" : detail.Code;
            Contact = detail.ContactPerson ?? "—";
            Phone = detail.Phone ?? "—";
            Email = detail.Email ?? "—";
            StatusText = detail.IsActive ? "Active" : "Inactive";
            TotalPurchased = detail.TotalPurchased;
            TotalPaid = detail.TotalPaid;
            Outstanding = detail.Outstanding;

            Purchases.Clear();
            foreach (var p in detail.Purchases) Purchases.Add(p);
            Payments.Clear();
            foreach (var p in detail.Payments) Payments.Add(p);

            var statement = await _suppliers.GetStatementAsync(supplierId, null, null);
            Statement.Clear();
            foreach (var line in statement.Lines) Statement.Add(line);
            OpeningBalance = statement.OpeningBalance;
            ClosingBalance = statement.ClosingBalance;

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Code));
            OnPropertyChanged(nameof(Contact));
            OnPropertyChanged(nameof(Phone));
            OnPropertyChanged(nameof(Email));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(TotalPurchased));
            OnPropertyChanged(nameof(TotalPaid));
            OnPropertyChanged(nameof(Outstanding));
            OnPropertyChanged(nameof(OpeningBalance));
            OnPropertyChanged(nameof(ClosingBalance));
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    /// <summary>Reload after a payment is recorded elsewhere (e.g. purchase details window).</summary>
    public Task RefreshAsync() => LoadAsync(_supplierId);
}