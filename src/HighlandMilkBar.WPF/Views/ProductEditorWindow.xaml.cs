using System.Globalization;
using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.Views;

public partial class ProductEditorWindow : Window
{
    private readonly ProductDetailDto? _editing;

    public CreateProductRequest? CreateResult { get; private set; }
    public UpdateProductRequest? UpdateResult { get; private set; }

    public ProductEditorWindow(IEnumerable<CategoryDto> categories, IEnumerable<UnitDto> units)
    {
        InitializeComponent();
        _editing = null;
        PopulateDropDowns(categories, units);
        SetAddMode();
    }

    public ProductEditorWindow(IEnumerable<CategoryDto> categories, IEnumerable<UnitDto> units, ProductDetailDto product)
    {
        InitializeComponent();
        _editing = product;
        PopulateDropDowns(categories, units);
        SetEditMode(product);
    }

    private void PopulateDropDowns(IEnumerable<CategoryDto> categories, IEnumerable<UnitDto> units)
    {
        CategoryInput.ItemsSource = categories.ToList();

        TypeInput.ItemsSource = Enum.GetValues<ProductType>()
            .Select(t => new TypeItem(t, DisplayName(t)))
            .ToList();
        TypeInput.DisplayMemberPath = nameof(TypeItem.Label);
        TypeInput.SelectedValuePath = nameof(TypeItem.Value);

        UnitInput.ItemsSource = units.ToList();
    }

    private void SetAddMode()
    {
        Title = "Add product";
        HeaderTitle.Text = "Add a product";
        HeaderSubtitle.Text = "Enter the product details. You can update these values later.";
        SaveButton.Content = "Save product";
        if (CategoryInput.Items.Count > 0) CategoryInput.SelectedIndex = 0;
        SelectType(ProductType.Packaged);
        if (UnitInput.Items.Count > 0) UnitInput.SelectedIndex = 0;
    }

    private void SetEditMode(ProductDetailDto editing)
    {
        Title = "Edit product";
        HeaderTitle.Text = "Edit product";
        HeaderSubtitle.Text = "Update the product details. Stock is changed through inventory, not here.";
        SaveButton.Content = "Save changes";

        NameInput.Text = editing.Name;
        SkuInput.Text = editing.Sku;
        BarcodeInput.Text = editing.Barcode ?? string.Empty;
        CostInput.Text = editing.CostPrice.ToString("0.##", CultureInfo.CurrentCulture);
        PriceInput.Text = editing.SellingPrice.ToString("0.##", CultureInfo.CurrentCulture);
        ReorderInput.Text = editing.ReorderLevel.ToString("0.##", CultureInfo.CurrentCulture);
        TrackInventoryInput.IsChecked = editing.TrackInventory;
        RequiresExpiryInput.IsChecked = editing.RequiresExpiryDate;
        IsActiveInput.IsChecked = editing.IsActive;

        if (CategoryInput.Items.Cast<CategoryDto>().FirstOrDefault(c => c.Id == editing.CategoryId) is { } cat)
            CategoryInput.SelectedItem = cat;
        SelectType(editing.Type);

        var unitList = (UnitInput.ItemsSource as IEnumerable<UnitDto>)?.ToList() ?? new List<UnitDto>();
        if (unitList.FirstOrDefault(u => u.Name == editing.Unit) is { } unit)
        {
            UnitInput.SelectedItem = unit;
        }
        else if (!string.IsNullOrWhiteSpace(editing.Unit))
        {
            var temp = new List<UnitDto>(unitList)
            {
                new UnitDto { Id = editing.BaseUnitId ?? 0, Name = editing.Unit, Symbol = editing.Unit }
            };
            UnitInput.ItemsSource = temp;
            UnitInput.SelectedIndex = temp.Count - 1;
        }
    }

    private void SelectType(ProductType type)
    {
        foreach (var item in TypeInput.Items)
        {
            if (item is TypeItem ti && ti.Value == type) { TypeInput.SelectedItem = ti; return; }
        }
    }

    private static string DisplayName(ProductType type) => type switch
    {
        ProductType.Packaged => "Packaged",
        ProductType.Ingredient => "Ingredient",
        ProductType.PreparedItem => "Prepared item",
        _ => type.ToString()
    };

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var name = NameInput.Text?.Trim() ?? string.Empty;
        var sku = SkuInput.Text?.Trim() ?? string.Empty;
        var barcode = string.IsNullOrWhiteSpace(BarcodeInput.Text) ? null : BarcodeInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(name)) { ShowError("A product name is required."); return; }
        if (string.IsNullOrWhiteSpace(sku)) { ShowError("A product code (SKU) is required."); return; }
        if (CategoryInput.SelectedValue is not int categoryId) { ShowError("Select a category."); return; }
        if (TypeInput.SelectedValue is not TypeItem typeItem) { ShowError("Select a product type."); return; }

        if (!TryReadDecimal(CostInput.Text, out var cost) || cost < 0) { ShowError("Enter a valid purchase price (zero or more)."); return; }
        if (!TryReadDecimal(PriceInput.Text, out var price) || price < 0) { ShowError("Enter a valid selling price (zero or more)."); return; }
        if (!TryReadDecimal(ReorderInput.Text, out var reorder) || reorder < 0) { ShowError("Enter a valid reorder level (zero or more)."); return; }

        var track = TrackInventoryInput.IsChecked == true;
        var requiresExpiry = track && RequiresExpiryInput.IsChecked == true;
        var unitName = (UnitInput.SelectedItem as UnitDto)?.Name ?? string.Empty;
        var unitId = (UnitInput.SelectedItem as UnitDto)?.Id;
        if (track && string.IsNullOrWhiteSpace(unitName)) { ShowError("Select a unit for an inventory-tracked product."); return; }

        if (_editing is null)
        {
            CreateResult = new CreateProductRequest
            {
                Name = name,
                Sku = sku,
                Barcode = barcode,
                CategoryId = categoryId,
                Type = typeItem.Value,
                Unit = unitName,
                BaseUnitId = track ? unitId : null,
                CostPrice = cost,
                SellingPrice = price,
                OpeningStock = 0m,
                ReorderLevel = reorder,
                TrackInventory = track,
                RequiresExpiryDate = requiresExpiry,
                IsActive = IsActiveInput.IsChecked == true
            };
        }
        else
        {
            UpdateResult = new UpdateProductRequest
            {
                Id = _editing.Id,
                Name = name,
                Sku = sku,
                Barcode = barcode,
                CategoryId = categoryId,
                Type = typeItem.Value,
                Unit = unitName,
                CostPrice = cost,
                SellingPrice = price,
                ReorderLevel = reorder,
                TrackInventory = track,
                RequiresExpiryDate = requiresExpiry,
                IsActive = IsActiveInput.IsChecked == true
            };
        }

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static bool TryReadDecimal(string text, out decimal value)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
           || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private sealed record TypeItem(ProductType Value, string Label);
}