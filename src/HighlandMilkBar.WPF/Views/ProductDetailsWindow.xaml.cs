using System.Windows;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.WPF.Views;

public partial class ProductDetailsWindow : Window
{
    public ProductDetailsWindow(ProductDetailDto product)
    {
        InitializeComponent();
        DataContext = product;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}