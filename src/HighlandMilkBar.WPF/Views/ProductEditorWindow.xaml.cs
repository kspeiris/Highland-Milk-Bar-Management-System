using System.Globalization;
using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.Views;

public partial class ProductEditorWindow : Window
{
    public CreateProductRequest? Result { get; private set; }

    public ProductEditorWindow(IEnumerable<CategoryDto> categories)
    {
        InitializeComponent();
        CategoryInput.ItemsSource = categories.ToList();
        CategoryInput.SelectedIndex = 0;
        TypeInput.ItemsSource = Enum.GetValues<ProductType>();
        TypeInput.SelectedItem = ProductType.Packaged;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameInput.Text) || string.IsNullOrWhiteSpace(SkuInput.Text))
        {
            MessageBox.Show("Product name and SKU are required.", "Check product details", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (CategoryInput.SelectedValue is not int categoryId || TypeInput.SelectedItem is not ProductType productType)
        {
            MessageBox.Show("Please select a category and product type.", "Check product details", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryReadDecimal(CostInput.Text, out var cost)
            || !TryReadDecimal(PriceInput.Text, out var price)
            || !TryReadDecimal(StockInput.Text, out var stock)
            || !TryReadDecimal(ReorderInput.Text, out var reorder)
            || cost < 0 || price < 0 || stock < 0 || reorder < 0)
        {
            MessageBox.Show("Enter valid non-negative numbers for all price and stock fields.", "Check numeric values", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new CreateProductRequest
        {
            Sku = SkuInput.Text,
            Name = NameInput.Text,
            CategoryId = categoryId,
            Type = productType,
            Unit = UnitInput.Text,
            CostPrice = cost,
            SellingPrice = price,
            OpeningStock = stock,
            ReorderLevel = reorder,
            TrackInventory = TrackInventoryInput.IsChecked == true
        };
        DialogResult = true;
    }

    private static bool TryReadDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
