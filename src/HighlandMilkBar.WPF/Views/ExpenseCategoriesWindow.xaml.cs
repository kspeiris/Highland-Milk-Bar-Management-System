using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.WPF.Views;

public partial class ExpenseCategoriesWindow : Window
{
    private readonly IExpenseService _expenses;

    public ExpenseCategoriesWindow(IExpenseService expenses)
    {
        InitializeComponent();
        _expenses = expenses;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        var categories = await _expenses.GetCategoriesAsync();
        Grid.ItemsSource = categories;
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var name = NewNameInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name)) { StatusText.Text = "Enter a category name."; return; }
        try
        {
            await _expenses.CreateCategoryAsync(new ExpenseCategoryRequest { Name = name, IsActive = true });
            NewNameInput.Text = string.Empty;
            await LoadAsync();
            StatusText.Text = $"Added '{name}'.";
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.ItemsSource is not System.Collections.IEnumerable items) return;
        try
        {
            foreach (var item in items)
            {
                if (item is not ExpenseCategoryDto c) continue;
                await _expenses.UpdateCategoryAsync(c.Id, new ExpenseCategoryRequest { Name = c.Name, IsActive = c.IsActive });
            }
            StatusText.Text = "Categories saved.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}