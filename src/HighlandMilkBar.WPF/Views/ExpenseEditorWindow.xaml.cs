using System.Globalization;
using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.Views;

public partial class ExpenseEditorWindow : Window
{
    private readonly IExpenseService _expenses;
    private readonly ExpenseDetailDto? _editing;

    public ExpenseCreateRequest? CreateResult { get; private set; }
    public ExpenseUpdateRequest? UpdateResult { get; private set; }

    public ExpenseEditorWindow(IExpenseService expenses)
    {
        InitializeComponent();
        _expenses = expenses;
        _editing = null;
        SetAddMode();
        Loaded += async (_, _) => await LoadCategoriesAsync();
    }

    public ExpenseEditorWindow(IExpenseService expenses, ExpenseDetailDto expense)
    {
        InitializeComponent();
        _expenses = expenses;
        _editing = expense;
        SetEditMode(expense);
        Loaded += async (_, _) => await LoadCategoriesAsync();
    }

    private async System.Threading.Tasks.Task LoadCategoriesAsync()
    {
        var categories = await _expenses.GetCategoriesAsync();
        CategoryInput.ItemsSource = categories;
        if (_editing?.CategoryId is { } id) CategoryInput.SelectedValue = id;
    }

    private void SetAddMode()
    {
        Title = "Add expense";
        HeaderTitle.Text = "Add an expense";
        HeaderSubtitle.Text = "Record an operating expense. This never affects sales or supplier balances.";
        SaveButton.Content = "Save expense";
        DateInput.SelectedDate = DateTime.Today;
    }

    private void SetEditMode(ExpenseDetailDto e)
    {
        Title = "Edit expense";
        HeaderTitle.Text = "Edit expense";
        HeaderSubtitle.Text = "Update the expense details. Linked cash movements are preserved.";
        SaveButton.Content = "Save changes";
        DescriptionInput.Text = e.Description;
        AmountInput.Text = e.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        DateInput.SelectedDate = e.ExpenseDate;
        MethodInput.SelectedIndex = (int)e.PaymentMethod;
        ReferenceInput.Text = e.ReferenceNumber ?? string.Empty;
        NotesInput.Text = e.Notes ?? string.Empty;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var description = DescriptionInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description)) { ShowError("A description is required."); return; }

        if (!decimal.TryParse(AmountInput.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            && !decimal.TryParse(AmountInput.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            ShowError("Enter a valid amount."); return;
        }
        if (amount <= 0) { ShowError("The amount must be greater than zero."); return; }

        var date = DateInput.SelectedDate ?? DateTime.Today;
        var method = (PaymentMethod)Math.Max(0, MethodInput.SelectedIndex);
        var categoryId = CategoryInput.SelectedValue is int cid && cid > 0 ? cid : (int?)null;
        string? Ref() => string.IsNullOrWhiteSpace(ReferenceInput.Text) ? null : ReferenceInput.Text.Trim();
        string? Notes() => string.IsNullOrWhiteSpace(NotesInput.Text) ? null : NotesInput.Text.Trim();

        if (_editing is null)
        {
            CreateResult = new ExpenseCreateRequest
            {
                CategoryId = categoryId,
                Description = description,
                Amount = amount,
                ExpenseDate = date,
                PaymentMethod = method,
                ReferenceNumber = Ref(),
                Notes = Notes()
            };
        }
        else
        {
            UpdateResult = new ExpenseUpdateRequest
            {
                Id = _editing.Id,
                CategoryId = categoryId,
                Description = description,
                Amount = amount,
                ExpenseDate = date,
                PaymentMethod = method,
                ReferenceNumber = Ref(),
                Notes = Notes()
            };
        }

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}