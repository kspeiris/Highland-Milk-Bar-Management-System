using System.Globalization;
using System.Windows;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.WPF.Views;

public partial class ExpenseDetailsWindow : Window
{
    public ExpenseDetailsWindow(ExpenseDetailDto e)
    {
        InitializeComponent();
        DescriptionText.Text = e.Description;
        AmountText.Text = $"LKR {e.Amount:N2}";
        DateText.Text = $"Date: {e.ExpenseDate:yyyy-MM-dd}";
        CategoryText.Text = $"Category: {(string.IsNullOrWhiteSpace(e.CategoryName) ? "Uncategorized" : e.CategoryName)}";
        MethodText.Text = $"Payment method: {e.PaymentMethodText}";
        ReferenceText.Text = $"Reference: {e.ReferenceNumber ?? "—"}";
        CreatedText.Text = $"Created: {e.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}";
        CashText.Text = e.CashSessionId is { } id ? $"Cash session: #{id}" : "Cash session: —";
        NotesText.Text = $"Notes: {e.Notes ?? "—"}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}