using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>A combo option that maps a label to an optional enum value.</summary>
public sealed class EnumOption<T> where T : struct
{
    public string Label { get; init; } = string.Empty;
    public T? Value { get; init; }
    public EnumOption() { }
    public EnumOption(string label, T? value) { Label = label; Value = value; }
}

/// <summary>Purchase workspace: dashboard KPIs, purchase list, filters and row actions.</summary>
public sealed class PurchasesViewModel : ObservableObject
{
    private readonly IPurchaseService _purchases;
    private readonly ISupplierService _suppliers;
    private string _searchText = string.Empty;
    private SupplierRowDto? _selectedSupplier;
    private EnumOption<PurchaseStatus>? _selectedReceiveStatus;
    private EnumOption<PaymentStatus>? _selectedPaymentStatus;
    private PurchaseRowDto? _selectedPurchase;
    private int _page = 1;
    private int _totalPages = 1;
    private int _totalCount;
    private bool _isLoading;
    private string _statusMessage = "Select a purchase to view actions.";

    // KPI fields
    private int _totalReceivedPurchases;
    private decimal _purchaseValue;
    private int _activeSuppliers;
    private decimal _outstandingPayments;
    private decimal _paymentsThisMonth;
    private int _purchasesThisMonth;

    public PurchasesViewModel(
        IPurchaseService purchases, ISupplierService suppliers, IProductService products, ISupplierPaymentService payments)
    {
        _purchases = purchases;
        _suppliers = suppliers;
        ProductService = products;
        PaymentService = payments;

        RefreshCommand = new RelayCommand(_ => _ = LoadAsync());
        SearchCommand = new RelayCommand(_ => { Page = 1; _ = SearchAsync(); });
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        PreviousPageCommand = new RelayCommand(_ => { if (Page > 1) { Page--; _ = SearchAsync(); } }, _ => Page > 1);
        NextPageCommand = new RelayCommand(_ => { if (Page < TotalPages) { Page++; _ = SearchAsync(); } }, _ => Page < TotalPages);

        NewCommand = new RelayCommand(_ => NewRequested?.Invoke());
        EditCommand = new RelayCommand(_ => Edit(), _ => SelectedPurchase is { ReceiveStatus: PurchaseStatus.Draft });
        ReceiveCommand = new RelayCommand(_ => Receive(), _ => SelectedPurchase is { ReceiveStatus: PurchaseStatus.Draft });
        CancelCommand = new RelayCommand(_ => Cancel(), _ => SelectedPurchase is { ReceiveStatus: PurchaseStatus.Draft });
        DetailsCommand = new RelayCommand(_ => Details(), _ => SelectedPurchase is not null);

        ReceiveStatusOptions =
        [
            new EnumOption<PurchaseStatus>("All statuses", null),
            new EnumOption<PurchaseStatus>("Draft", PurchaseStatus.Draft),
            new EnumOption<PurchaseStatus>("Received", PurchaseStatus.Received),
            new EnumOption<PurchaseStatus>("Cancelled", PurchaseStatus.Cancelled)
        ];
        PaymentStatusOptions =
        [
            new EnumOption<PaymentStatus>("All payment states", null),
            new EnumOption<PaymentStatus>("Unpaid", PaymentStatus.Unpaid),
            new EnumOption<PaymentStatus>("Partially paid", PaymentStatus.PartiallyPaid),
            new EnumOption<PaymentStatus>("Paid", PaymentStatus.Paid)
        ];
        SelectedReceiveStatus = ReceiveStatusOptions[0];
        SelectedPaymentStatus = PaymentStatusOptions[0];
    }

    public ObservableCollection<PurchaseRowDto> Purchases { get; } = new();
    public ObservableCollection<SupplierRowDto> Suppliers { get; } = new();
    public ObservableCollection<MonthlySpendDto> MonthlyTrend { get; } = new();

    public IPurchaseService PurchaseService => _purchases;
    public ISupplierService SupplierService => _suppliers;
    public IProductService ProductService { get; }
    public ISupplierPaymentService PaymentService { get; }

    public ObservableCollection<EnumOption<PurchaseStatus>> ReceiveStatusOptions { get; }
    public ObservableCollection<EnumOption<PaymentStatus>> PaymentStatusOptions { get; }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand ReceiveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DetailsCommand { get; }

    public event Action? NewRequested;
    public event Action<PurchaseRowDto>? EditRequested;
    public event Action<PurchaseRowDto>? ReceiveRequested;
    public event Action<PurchaseRowDto>? CancelRequested;
    public event Action<PurchaseRowDto>? DetailsRequested;

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public SupplierRowDto? SelectedSupplier { get => _selectedSupplier; set => SetProperty(ref _selectedSupplier, value); }
    public EnumOption<PurchaseStatus>? SelectedReceiveStatus { get => _selectedReceiveStatus; set => SetProperty(ref _selectedReceiveStatus, value); }
    public EnumOption<PaymentStatus>? SelectedPaymentStatus { get => _selectedPaymentStatus; set => SetProperty(ref _selectedPaymentStatus, value); }

    public PurchaseRowDto? SelectedPurchase
    {
        get => _selectedPurchase;
        set
        {
            if (SetProperty(ref _selectedPurchase, value))
            {
                StatusMessage = value is null
                    ? "Select a purchase to view actions."
                    : $"{value.PurchaseNumber} · {value.SupplierName} · {value.ReceiveStatus} · {value.PaymentStatus}";
                EditCommand.RaiseCanExecuteChanged();
                ReceiveCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
                DetailsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int Page { get => _page; private set { if (SetProperty(ref _page, value)) { OnPropertyChanged(nameof(PageLabel)); NextPageCommand.RaiseCanExecuteChanged(); PreviousPageCommand.RaiseCanExecuteChanged(); } } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { OnPropertyChanged(nameof(PageLabel)); NextPageCommand.RaiseCanExecuteChanged(); PreviousPageCommand.RaiseCanExecuteChanged(); } } }
    public int TotalCount { get => _totalCount; private set => SetProperty(ref _totalCount, value); }
    public string PageLabel => $"Page {Page} of {TotalPages} ({TotalCount} purchases)";
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public int TotalReceivedPurchases { get => _totalReceivedPurchases; private set => SetProperty(ref _totalReceivedPurchases, value); }
    public decimal PurchaseValue { get => _purchaseValue; private set => SetProperty(ref _purchaseValue, value); }
    public int ActiveSuppliers { get => _activeSuppliers; private set => SetProperty(ref _activeSuppliers, value); }
    public decimal OutstandingPayments { get => _outstandingPayments; private set => SetProperty(ref _outstandingPayments, value); }
    public decimal PaymentsThisMonth { get => _paymentsThisMonth; private set => SetProperty(ref _paymentsThisMonth, value); }
    public int PurchasesThisMonth { get => _purchasesThisMonth; private set => SetProperty(ref _purchasesThisMonth, value); }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var dashboard = await _purchases.GetDashboardAsync();
            TotalReceivedPurchases = dashboard.TotalReceivedPurchases;
            PurchaseValue = dashboard.PurchaseValue;
            ActiveSuppliers = dashboard.ActiveSuppliers;
            OutstandingPayments = dashboard.OutstandingPayments;
            PaymentsThisMonth = dashboard.PaymentsThisMonth;
            PurchasesThisMonth = dashboard.PurchasesThisMonth;

            MonthlyTrend.Clear();
            foreach (var m in dashboard.MonthlyTrend) MonthlyTrend.Add(m);

            var suppliers = await _suppliers.SearchAsync(new SupplierFilter { PageSize = 500 });
            Suppliers.Clear();
            Suppliers.Add(new SupplierRowDto { Id = 0, Name = "All suppliers" });
            foreach (var s in suppliers.Items) Suppliers.Add(s);
            if (SelectedSupplier is null) SelectedSupplier = Suppliers[0];

            await SearchAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SearchAsync()
    {
        var result = await _purchases.SearchAsync(new PurchaseFilter
        {
            Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            SupplierId = SelectedSupplier is { Id: > 0 } s ? s.Id : null,
            ReceiveStatus = SelectedReceiveStatus?.Value,
            PaymentStatus = SelectedPaymentStatus?.Value,
            Page = Page,
            PageSize = 25
        });
        Purchases.Clear();
        foreach (var p in result.Items) Purchases.Add(p);
        TotalCount = result.TotalCount;
        TotalPages = Math.Max(1, result.TotalPages);
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        if (Suppliers.Count > 0) SelectedSupplier = Suppliers[0];
        SelectedReceiveStatus = ReceiveStatusOptions[0];
        SelectedPaymentStatus = PaymentStatusOptions[0];
        Page = 1;
        _ = SearchAsync();
    }

    private void Edit() { if (SelectedPurchase is { } p) EditRequested?.Invoke(p); }
    private void Receive() { if (SelectedPurchase is { } p) ReceiveRequested?.Invoke(p); }
    private void Cancel() { if (SelectedPurchase is { } p) CancelRequested?.Invoke(p); }
    private void Details() { if (SelectedPurchase is { } p) DetailsRequested?.Invoke(p); }

    public Task<ReceivePurchaseResult> ReceiveAsync(int id) => SaveAndReload(() => _purchases.ReceiveAsync(id));

    public async Task CancelAsync(int id)
    {
        await _purchases.CancelAsync(id);
        await SearchAsync();
        await LoadDashboardOnlyAsync();
    }

    private async Task<T> SaveAndReload<T>(Func<Task<T>> action)
    {
        var result = await action();
        await SearchAsync();
        await LoadDashboardOnlyAsync();
        return result;
    }

    private async Task LoadDashboardOnlyAsync()
    {
        var dashboard = await _purchases.GetDashboardAsync();
        TotalReceivedPurchases = dashboard.TotalReceivedPurchases;
        PurchaseValue = dashboard.PurchaseValue;
        OutstandingPayments = dashboard.OutstandingPayments;
        PaymentsThisMonth = dashboard.PaymentsThisMonth;
        PurchasesThisMonth = dashboard.PurchasesThisMonth;
    }
}

/// <summary>One editable line on the purchase entry grid.</summary>
public sealed class PurchaseEditorLine : ObservableObject
{
    private int _productId;
    private string _productName = string.Empty;
    private decimal _quantity = 1m;
    private string _unit = string.Empty;
    private decimal _unitCost;
    private decimal _discountAmount;
    private string _batchNumber = string.Empty;
    private DateTime? _expiryDate;
    private bool _requiresExpiry;

    public int ProductId { get => _productId; set => SetProperty(ref _productId, value); }
    public string ProductName { get => _productName; set => SetProperty(ref _productName, value); }
    public decimal Quantity { get => _quantity; set { if (SetProperty(ref _quantity, value)) OnPropertyChanged(nameof(LineTotal)); } }
    public string Unit { get => _unit; set => SetProperty(ref _unit, value); }
    public decimal UnitCost { get => _unitCost; set { if (SetProperty(ref _unitCost, value)) OnPropertyChanged(nameof(LineTotal)); } }
    public decimal DiscountAmount { get => _discountAmount; set { if (SetProperty(ref _discountAmount, value)) OnPropertyChanged(nameof(LineTotal)); } }
    public string BatchNumber { get => _batchNumber; set => SetProperty(ref _batchNumber, value); }
    public DateTime? ExpiryDate { get => _expiryDate; set => SetProperty(ref _expiryDate, value); }
    public bool RequiresExpiry { get => _requiresExpiry; set => SetProperty(ref _requiresExpiry, value); }
    public bool IsInventoryTracked { get; set; }
    public decimal LineTotal => PurchaseFinancials.LineTotal(Quantity, UnitCost, DiscountAmount);
}

/// <summary>Create/edit a purchase draft with an editable line grid and live totals.</summary>
public sealed class PurchaseEditorViewModel : ObservableObject
{
    private readonly IPurchaseService _purchases;
    private readonly ISupplierService _suppliers;
    private readonly IProductService _products;
    private readonly PurchaseDetailDto? _editing;

    private SupplierRowDto? _selectedSupplier;
    private string _invoiceNumber = string.Empty;
    private DateTime _purchaseDate = DateTime.Today;
    private DateTime? _dueDate;
    private decimal _discountAmount;
    private decimal _additionalCharges;
    private string _notes = string.Empty;
    private bool _isBusy;
    private string? _error;
    private string _title = "New purchase";
    private string _statusLabel = "New Purchase";

    public PurchaseEditorViewModel(
        IPurchaseService purchases, ISupplierService suppliers, IProductService products, PurchaseDetailDto? editing = null)
    {
        _purchases = purchases;
        _suppliers = suppliers;
        _products = products;
        _editing = editing;
        AddLineCommand = new RelayCommand(_ => AddLine());
        RemoveLineCommand = new RelayCommand(_ => RemoveLine());
        SaveDraftCommand = new RelayCommand(_ => _ = Save(false), _ => !IsBusy);
        ReceiveCommand = new RelayCommand(_ => _ = Save(true), _ => !IsBusy);
    }

    public ObservableCollection<SupplierRowDto> Suppliers { get; } = new();
    public ObservableCollection<ProductDto> Products { get; } = new();
    public ObservableCollection<PurchaseEditorLine> Lines { get; } = new();

    public RelayCommand AddLineCommand { get; }
    public RelayCommand RemoveLineCommand { get; }
    public RelayCommand SaveDraftCommand { get; }
    public RelayCommand ReceiveCommand { get; }

    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string StatusLabel { get => _statusLabel; private set => SetProperty(ref _statusLabel, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { SaveDraftCommand.RaiseCanExecuteChanged(); ReceiveCommand.RaiseCanExecuteChanged(); } } }
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(_error);

    public SupplierRowDto? SelectedSupplier { get => _selectedSupplier; set => SetProperty(ref _selectedSupplier, value); }
    public string InvoiceNumber { get => _invoiceNumber; set => SetProperty(ref _invoiceNumber, value); }
    public DateTime PurchaseDate { get => _purchaseDate; set { if (SetProperty(ref _purchaseDate, value)) OnPropertyChanged(nameof(PurchaseDateText)); } }
    public string PurchaseDateText { get => _purchaseDate.ToString("yyyy-MM-dd"); set { if (DateTime.TryParse(value, CultureInfo.InvariantCulture, out var d)) PurchaseDate = d.Date; } }
    public DateTime? DueDate { get => _dueDate; set { if (SetProperty(ref _dueDate, value)) OnPropertyChanged(nameof(DueDateText)); } }
    public string DueDateText { get => _dueDate?.ToString("yyyy-MM-dd") ?? string.Empty; set { _dueDate = DateTime.TryParse(value, CultureInfo.InvariantCulture, out var d) ? d.Date : null; OnPropertyChanged(); OnPropertyChanged(nameof(DueDate)); } }
    public decimal DiscountAmount { get => _discountAmount; set { if (SetProperty(ref _discountAmount, value)) RecalculateTotals(); } }
    public decimal AdditionalCharges { get => _additionalCharges; set { if (SetProperty(ref _additionalCharges, value)) RecalculateTotals(); } }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public decimal Subtotal { get; private set; }
    public decimal GrandTotal { get; private set; }
    public bool CanReceive => _editing is null || _editing.CanReceive;

    public event Action<PurchaseDetailDto>? Completed;

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var suppliers = await _suppliers.SearchAsync(new SupplierFilter { IsActive = true, PageSize = 500 });
            Suppliers.Clear();
            foreach (var s in suppliers.Items) Suppliers.Add(s);

            var products = await _products.SearchAsync(new ProductFilter { PageSize = 500 });
            Products.Clear();
            foreach (var p in products.Items.Where(p => p.IsActive && p.TrackInventory)) Products.Add(p);

            if (_editing is not null)
            {
                Title = $"Edit {(_editing.CanEdit ? "draft" : "purchase")} { _editing.PurchaseNumber}";
                StatusLabel = _editing.ReceiveStatus.ToString();
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == _editing.SupplierId);
                InvoiceNumber = _editing.InvoiceNumber ?? string.Empty;
                PurchaseDate = _editing.PurchaseDate;
                DueDate = _editing.DueDate;
                DiscountAmount = _editing.DiscountAmount;
                AdditionalCharges = _editing.AdditionalCharges;
                Notes = _editing.Notes ?? string.Empty;
                Lines.Clear();
                foreach (var i in _editing.Items)
                    Lines.Add(new PurchaseEditorLine
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        UnitCost = i.UnitCost,
                        DiscountAmount = i.DiscountAmount,
                        BatchNumber = i.BatchNumber ?? string.Empty,
                        ExpiryDate = i.ExpiryDate,
                        RequiresExpiry = i.RequiresExpiryDate,
                        IsInventoryTracked = i.TrackInventory
                    });
            }

            OnPropertyChanged(nameof(CanReceive));
            RecalculateTotals();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddLine()
    {
        var line = new PurchaseEditorLine();
        Lines.Add(line);
        RecalculateTotals();
    }

    private void RemoveLine()
    {
        // Remove is driven from the view via SelectedLine; default to the last line when none chosen.
        SelectedLine ??= Lines.Count > 0 ? Lines[^1] : null;
        if (SelectedLine is { } line && Lines.Remove(line))
        {
            SelectedLine = null;
            RecalculateTotals();
        }
    }

    private PurchaseEditorLine? _selectedLine;
    public PurchaseEditorLine? SelectedLine { get => _selectedLine; set => SetProperty(ref _selectedLine, value); }

    public void LineChanged() => RecalculateTotals();

    private void RecalculateTotals()
    {
        Subtotal = Lines.Sum(l => l.LineTotal);
        GrandTotal = PurchaseFinancials.GrandTotal(Subtotal, DiscountAmount, AdditionalCharges);
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(GrandTotal));
    }

    private async Task Save(bool receive)
    {
        Error = null;
        if (SelectedSupplier is not { Id: > 0 } supplier)
        {
            Error = "Select a supplier.";
            return;
        }
        if (Lines.Count == 0)
        {
            Error = "Add at least one item.";
            return;
        }
        foreach (var line in Lines)
        {
            if (line.ProductId <= 0) { Error = "Choose a product for every line."; return; }
            if (line.Quantity <= 0) { Error = "Every line needs a positive quantity."; return; }
            if (line.UnitCost < 0) { Error = "Unit cost cannot be negative."; return; }
        }

        var lineRequests = Lines.Select(l => new PurchaseLineRequest
        {
            ProductId = l.ProductId,
            Quantity = l.Quantity,
            Unit = l.Unit,
            UnitCost = l.UnitCost,
            DiscountAmount = l.DiscountAmount,
            BatchNumber = string.IsNullOrWhiteSpace(l.BatchNumber) ? null : l.BatchNumber.Trim(),
            ExpiryDate = l.ExpiryDate
        }).ToList();

        IsBusy = true;
        try
        {
            PurchaseDetailDto detail;
            if (_editing is null)
                detail = await _purchases.CreateAsync(new CreatePurchaseRequest
                {
                    SupplierId = supplier.Id,
                    InvoiceNumber = string.IsNullOrWhiteSpace(InvoiceNumber) ? null : InvoiceNumber.Trim(),
                    PurchaseDate = PurchaseDate,
                    DueDate = DueDate,
                    DiscountAmount = DiscountAmount,
                    AdditionalCharges = AdditionalCharges,
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    Items = lineRequests
                });
            else
                detail = await _purchases.UpdateAsync(new UpdatePurchaseRequest
                {
                    Id = _editing.Id,
                    SupplierId = supplier.Id,
                    InvoiceNumber = string.IsNullOrWhiteSpace(InvoiceNumber) ? null : InvoiceNumber.Trim(),
                    PurchaseDate = PurchaseDate,
                    DueDate = DueDate,
                    DiscountAmount = DiscountAmount,
                    AdditionalCharges = AdditionalCharges,
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    Items = lineRequests
                });

            if (receive && detail.CanReceive)
                detail = await ReceiveAndReloadAsync(detail.Id);

            Completed?.Invoke(detail);
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

    private async Task<PurchaseDetailDto> ReceiveAndReloadAsync(int id)
    {
        await _purchases.ReceiveAsync(id);
        return (await _purchases.GetByIdAsync(id))!;
    }
}

/// <summary>Purchase details + receiving + payment recording for a single purchase.</summary>
public sealed class PurchaseDetailsViewModel : ObservableObject
{
    private readonly IPurchaseService _purchases;
    private readonly ISupplierPaymentService _payments;
    private int _purchaseId;
    private decimal _paymentAmount;
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    private string _paymentReference = string.Empty;
    private bool _isBusy;
    private string? _error;
    private string? _info;

    public PurchaseDetailsViewModel(IPurchaseService purchases, ISupplierPaymentService payments)
    {
        _purchases = purchases;
        _payments = payments;
        RecordPaymentCommand = new RelayCommand(_ => _ = RecordPayment(), _ => CanRecordPayment);
        ReceiveCommand = new RelayCommand(_ => _ = Receive(), _ => CanReceive);
    }

    public ObservableCollection<PurchaseItemDto> Items { get; } = new();
    public ObservableCollection<SupplierPaymentDto> Payments { get; } = new();

    public RelayCommand RecordPaymentCommand { get; }
    public RelayCommand ReceiveCommand { get; }

    public int PurchaseId => _purchaseId;
    public string PurchaseNumber { get; private set; } = string.Empty;
    public string SupplierName { get; private set; } = string.Empty;
    public string InvoiceNumber { get; private set; } = string.Empty;
    public string PurchaseDateText { get; private set; } = string.Empty;
    public string ReceiveStatusText { get; private set; } = string.Empty;
    public string PaymentStatusText { get; private set; } = string.Empty;
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal AdditionalCharges { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal Outstanding { get; private set; }
    public bool CanReceive { get; private set; }
    public bool CanRecordPayment { get; private set; }

    public decimal PaymentAmount { get => _paymentAmount; set => SetProperty(ref _paymentAmount, value); }
    public PaymentMethod PaymentMethod { get => _paymentMethod; set => SetProperty(ref _paymentMethod, value); }
    public string PaymentReference { get => _paymentReference; set => SetProperty(ref _paymentReference, value); }
    public Array PaymentMethods => Enum.GetValues<PaymentMethod>();
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string? Error { get => _error; private set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public string? Info { get => _info; private set { if (SetProperty(ref _info, value)) OnPropertyChanged(nameof(HasInfo)); } }
    public bool HasError => !string.IsNullOrEmpty(_error);
    public bool HasInfo => !string.IsNullOrEmpty(_info);

    public async Task LoadAsync(int purchaseId)
    {
        _purchaseId = purchaseId;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var detail = await _purchases.GetByIdAsync(_purchaseId);
        if (detail is null) return;

        PurchaseNumber = detail.PurchaseNumber;
        SupplierName = detail.SupplierName;
        InvoiceNumber = string.IsNullOrWhiteSpace(detail.InvoiceNumber) ? "—" : detail.InvoiceNumber!;
        PurchaseDateText = detail.PurchaseDate.ToString("yyyy-MM-dd");
        ReceiveStatusText = detail.ReceiveStatus.ToString();
        PaymentStatusText = detail.PaymentStatus.ToString();
        Subtotal = detail.Subtotal;
        DiscountAmount = detail.DiscountAmount;
        AdditionalCharges = detail.AdditionalCharges;
        TotalAmount = detail.TotalAmount;
        AmountPaid = detail.AmountPaid;
        Outstanding = detail.Outstanding;
        CanReceive = detail.CanReceive;
        CanRecordPayment = detail.ReceiveStatus == PurchaseStatus.Received && detail.Outstanding > 0;
        PaymentAmount = detail.Outstanding;

        Items.Clear();
        foreach (var i in detail.Items) Items.Add(i);
        Payments.Clear();
        foreach (var p in detail.Payments) Payments.Add(p);

        OnPropertyChanged(nameof(PurchaseNumber));
        OnPropertyChanged(nameof(SupplierName));
        OnPropertyChanged(nameof(InvoiceNumber));
        OnPropertyChanged(nameof(PurchaseDateText));
        OnPropertyChanged(nameof(ReceiveStatusText));
        OnPropertyChanged(nameof(PaymentStatusText));
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DiscountAmount));
        OnPropertyChanged(nameof(AdditionalCharges));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(AmountPaid));
        OnPropertyChanged(nameof(Outstanding));
        OnPropertyChanged(nameof(CanReceive));
        OnPropertyChanged(nameof(CanRecordPayment));
        RecordPaymentCommand.RaiseCanExecuteChanged();
        ReceiveCommand.RaiseCanExecuteChanged();
    }

    private async Task Receive()
    {
        IsBusy = true;
        Error = null;
        try
        {
            var result = await _purchases.ReceiveAsync(_purchaseId);
            Info = $"Received {result.TotalQuantityReceived:0.##} units into {result.BatchesCreated} batch(es).";
            await ReloadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RecordPayment()
    {
        IsBusy = true;
        Error = null;
        Info = null;
        try
        {
            await _payments.RecordAsync(new RecordPaymentRequest
            {
                PurchaseId = _purchaseId,
                Amount = PaymentAmount,
                Method = PaymentMethod,
                PaidAt = DateTime.Now,
                Reference = string.IsNullOrWhiteSpace(PaymentReference) ? null : PaymentReference.Trim()
            });
            PaymentReference = string.Empty;
            Info = "Payment recorded.";
            await ReloadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}