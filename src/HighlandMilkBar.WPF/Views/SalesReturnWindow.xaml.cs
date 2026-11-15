using System.Globalization;
using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class SalesReturnWindow : Window
{
    private readonly SaleDetailDto _detail;
    private readonly SalesHistoryViewModel _history;
    private readonly List<ReturnRow> _rows = new();

    public SalesReturnWindow(SaleDetailDto detail, SalesHistoryViewModel history)
    {
        InitializeComponent();
        _detail = detail;
        _history = history;

        HeaderText.Text = $"Return against {detail.ReceiptNumber}";
        SubHeaderText.Text = $"{detail.SoldAt:yyyy-MM-dd HH:mm}  •  Sale total LKR {detail.TotalAmount:0.00}";

        foreach (var item in detail.Items)
        {
            _rows.Add(new ReturnRow
            {
                SaleItemId = item.SaleItemId,
                Name = item.ItemNameSnapshot,
                UnitPrice = item.UnitPrice,
                SoldQuantity = item.Quantity
            });
        }

        foreach (var row in _rows) row.RefundChanged += UpdateRefund;
        ItemsGrid.ItemsSource = _rows;
        UpdateRefund();
    }

    public string Reason { get; set; } = string.Empty;

    private void UpdateRefund()
    {
        var total = _rows.Sum(r => r.LineTotal);
        RefundText.Text = $"LKR {total:0.00}";
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;

        var lines = new List<ReturnLineRequest>();
        foreach (var row in _rows)
        {
            if (!TryParse(row.ReturnQuantityText, out var qty) || qty <= 0) continue;
            if (qty > row.SoldQuantity)
            {
                StatusText.Text = $"Return quantity for '{row.Name}' exceeds the sold quantity.";
                return;
            }
            lines.Add(new ReturnLineRequest
            {
                SaleItemId = row.SaleItemId,
                Quantity = qty,
                Condition = row.ConditionValue
            });
        }

        if (lines.Count == 0)
        {
            StatusText.Text = "Enter a return quantity for at least one item.";
            return;
        }

        try
        {
            var result = await _history.SubmitReturnAsync(new SalesReturnRequest
            {
                SaleId = _detail.Id,
                Reason = string.IsNullOrWhiteSpace(Reason) ? null : Reason,
                Lines = lines
            });

            MessageBox.Show(this, $"Return {result.ReturnNumber} recorded. Refund LKR {result.TotalAmount:0.00}.",
                "Return completed", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static bool TryParse(string? text, out decimal value)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
           || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}

public sealed class ReturnRow : ObservableObject
{
    private string _returnQuantityText = string.Empty;
    private ReturnConditionOption? _condition;

    public int SaleItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public decimal SoldQuantity { get; init; }

    public static IReadOnlyList<ReturnConditionOption> Conditions { get; } = new List<ReturnConditionOption>
    {
        new(ReturnCondition.Restock, "Sellable"),
        new(ReturnCondition.Damaged, "Damaged"),
        new(ReturnCondition.Expired, "Expired"),
        new(ReturnCondition.NotForResale, "Not for resale")
    };

    public IEnumerable<ReturnConditionOption> ConditionOptions => Conditions;

    public string ReturnQuantityText
    {
        get => _returnQuantityText;
        set
        {
            if (SetProperty(ref _returnQuantityText, value))
            {
                OnPropertyChanged(nameof(LineTotal));
                RefundChanged?.Invoke();
            }
        }
    }

    public ReturnConditionOption? Condition
    {
        get => _condition;
        set { if (SetProperty(ref _condition, value)) OnPropertyChanged(nameof(ConditionValue)); }
    }

    public ReturnCondition ConditionValue => _condition?.Value ?? ReturnCondition.Restock;

    public decimal LineTotal
    {
        get
        {
            var qty = decimal.TryParse(_returnQuantityText, NumberStyles.Number, CultureInfo.CurrentCulture, out var q)
                ? q : 0m;
            return qty * UnitPrice;
        }
    }

    public event Action? RefundChanged;
}

public sealed record ReturnConditionOption(ReturnCondition Value, string Label);