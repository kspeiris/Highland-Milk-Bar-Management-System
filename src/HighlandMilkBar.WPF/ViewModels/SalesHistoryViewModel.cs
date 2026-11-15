using System.Collections.ObjectModel;
using System.Windows.Input;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.ViewModels;

/// <summary>Sales history: search, filter, paging, and actions (details, reprint, return).</summary>
public sealed class SalesHistoryViewModel : ObservableObject
{
    private readonly ISalesQueryService _sales;
    private readonly ISalesService _salesCommand;
    private readonly IReceiptService _receipts;
    private const int PageSize = 20;

    private string _searchText = string.Empty;
    private StatusOption? _selectedStatus;
    private PaymentOption? _selectedPayment;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private SaleListRowDto? _selectedSale;
    private int _page = 1;
    private int _totalCount;
    private int _totalPages;
    private bool _isLoading;
    private string _statusMessage = "Loading sales…";

    public SalesHistoryViewModel(ISalesQueryService sales, ISalesService salesCommand, IReceiptService receipts)
    {
        _sales = sales;
        _salesCommand = salesCommand;
        _receipts = receipts;

        foreach (var status in Enum.GetValues<SaleStatus>())
            StatusOptions.Add(new StatusOption(status.ToString(), status));
        PaymentOptions.Add(new PaymentOption("All methods", null));
        foreach (var method in Enum.GetValues<PaymentMethod>())
            PaymentOptions.Add(new PaymentOption(method.ToString(), method));

        SearchCommand = new RelayCommand(_ => _ = SearchAsync());
        ClearCommand = new RelayCommand(_ => _ = ClearAsync());
        RefreshCommand = new RelayCommand(_ => _ = SearchAsync());
        NextCommand = new RelayCommand(_ => _ = GoToAsync(Page + 1), _ => HasNextPage);
        PreviousCommand = new RelayCommand(_ => _ = GoToAsync(Page - 1), _ => HasPreviousPage);
        DetailsCommand = new RelayCommand(_ => _ = OpenDetailsAsync(), _ => SelectedSale is not null);
        ReprintCommand = new RelayCommand(_ => _ = ReprintAsync(), _ => SelectedSale is not null);
        ReturnCommand = new RelayCommand(_ => _ = OpenReturnAsync(), _ => SelectedSale is not null);
    }

    public ObservableCollection<SaleListRowDto> Rows { get; } = new();
    public ObservableCollection<StatusOption> StatusOptions { get; } = new();
    public ObservableCollection<PaymentOption> PaymentOptions { get; } = new();

    public RelayCommand SearchCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand PreviousCommand { get; }
    public RelayCommand DetailsCommand { get; }
    public RelayCommand ReprintCommand { get; }
    public RelayCommand ReturnCommand { get; }

    public event Action<SaleDetailDto>? DetailsRequested;
    public event Action<ReceiptDocument>? ReceiptRequested;
    public event Action<SaleDetailDto>? ReturnRequested;

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public StatusOption? SelectedStatus { get => _selectedStatus; set => SetProperty(ref _selectedStatus, value); }
    public PaymentOption? SelectedPayment { get => _selectedPayment; set => SetProperty(ref _selectedPayment, value); }
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public SaleListRowDto? SelectedSale
    {
        get => _selectedSale;
        set
        {
            if (SetProperty(ref _selectedSale, value))
            {
                DetailsCommand.RaiseCanExecuteChanged();
                ReprintCommand.RaiseCanExecuteChanged();
                ReturnCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public int Page
    {
        get => _page;
        private set { if (SetProperty(ref _page, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPaging(); } }
    }
    public int TotalCount { get => _totalCount; private set { if (SetProperty(ref _totalCount, value)) OnPropertyChanged(nameof(PageLabel)); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) { OnPropertyChanged(nameof(PageLabel)); RefreshPaging(); } } }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
    public string PageLabel => TotalPages == 0 ? "Page 0 of 0" : $"Page {Page} of {TotalPages}  •  {TotalCount} sale(s)";

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        if (SelectedStatus is null) SelectedStatus = StatusOptions.FirstOrDefault();
        if (SelectedPayment is null) SelectedPayment = PaymentOptions[0];
        await SearchAsync();
    }

    public async Task SearchAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var filter = new SaleSearchFilter
            {
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                Status = SelectedStatus?.Value,
                PaymentMethod = SelectedPayment?.Value,
                From = FromDate,
                To = ToDate,
                Page = 1,
                PageSize = PageSize
            };
            var result = await _sales.SearchAsync(filter);
            Rows.Clear();
            foreach (var row in result.Items) Rows.Add(row);
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
            Page = result.Page;
            StatusMessage = result.TotalCount == 0 ? "No sales match the filters." : $"{result.TotalCount} sale(s) found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load sales: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GoToAsync(int page)
    {
        if (page < 1 || page > TotalPages || page == Page) return;
        IsLoading = true;
        try
        {
            var filter = new SaleSearchFilter
            {
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                Status = SelectedStatus?.Value,
                PaymentMethod = SelectedPayment?.Value,
                From = FromDate,
                To = ToDate,
                Page = page,
                PageSize = PageSize
            };
            var result = await _sales.SearchAsync(filter);
            Rows.Clear();
            foreach (var row in result.Items) Rows.Add(row);
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
            Page = result.Page;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load sales: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ClearAsync()
    {
        SearchText = string.Empty;
        SelectedStatus = StatusOptions.FirstOrDefault();
        SelectedPayment = PaymentOptions[0];
        FromDate = null;
        ToDate = null;
        await SearchAsync();
    }

    private async Task<SaleDetailDto?> LoadDetailAsync()
    {
        if (SelectedSale is null) return null;
        return await _sales.GetByIdAsync(SelectedSale.Id);
    }

    private async Task OpenDetailsAsync()
    {
        var detail = await LoadDetailAsync();
        if (detail is not null) DetailsRequested?.Invoke(detail);
    }

    private async Task ReprintAsync()
    {
        var detail = await LoadDetailAsync();
        if (detail is null) return;
        try
        {
            var receipt = await _receipts.CreateForSaleAsync(detail.Id, ReceiptFormat.Thermal80);
            ReceiptRequested?.Invoke(receipt);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not build receipt: {ex.Message}";
        }
    }

    private async Task OpenReturnAsync()
    {
        var detail = await LoadDetailAsync();
        if (detail is not null) ReturnRequested?.Invoke(detail);
    }

    private void RefreshPaging()
    {
        NextCommand.RaiseCanExecuteChanged();
        PreviousCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Processes a return and refreshes the history. Returns the result for the caller.</summary>
    public async Task<SalesReturnResult> SubmitReturnAsync(SalesReturnRequest request)
    {
        var result = await _salesCommand.ProcessReturnAsync(request);
        await SearchAsync();
        return result;
    }

    public sealed record StatusOption(string Label, SaleStatus Value);
    public sealed record PaymentOption(string Label, PaymentMethod? Value);
}