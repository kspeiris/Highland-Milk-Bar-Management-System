using System.Windows;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.WPF.Views;

public partial class SaleDetailsWindow : Window
{
    public SaleDetailsWindow(SaleDetailDto detail)
    {
        InitializeComponent();

        InvoiceText.Text = detail.ReceiptNumber;
        DateText.Text = detail.SoldAt.ToString("yyyy-MM-dd HH:mm");
        CustomerText.Text = string.IsNullOrWhiteSpace(detail.CustomerName) ? "Walk-in customer" : detail.CustomerName;
        StatusText.Text = detail.StatusText;
        TotalText.Text = $"LKR {detail.TotalAmount:0.00}";
        PaidText.Text = $"Paid LKR {detail.AmountPaid:0.00}  •  Change LKR {detail.Change:0.00}";

        ItemsGrid.ItemsSource = detail.Items;
        PaymentsGrid.ItemsSource = detail.Payments;
        ReturnsGrid.ItemsSource = detail.Returns;
    }
}