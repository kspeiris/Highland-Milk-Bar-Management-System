using System.Windows;
using System.Windows.Controls;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.WPF.Views;

public partial class SupplierEditorWindow : Window
{
    private readonly SupplierDetailDto? _editing;

    public CreateSupplierRequest? CreateResult { get; private set; }
    public UpdateSupplierRequest? UpdateResult { get; private set; }

    public SupplierEditorWindow()
    {
        InitializeComponent();
        _editing = null;
        SetAddMode();
    }

    public SupplierEditorWindow(SupplierDetailDto supplier)
    {
        InitializeComponent();
        _editing = supplier;
        SetEditMode(supplier);
    }

    private void SetAddMode()
    {
        Title = "Add supplier";
        HeaderTitle.Text = "Add a supplier";
        HeaderSubtitle.Text = "Enter the supplier details. You can update these values later.";
        SaveButton.Content = "Save supplier";
    }

    private void SetEditMode(SupplierDetailDto s)
    {
        Title = "Edit supplier";
        HeaderTitle.Text = "Edit supplier";
        HeaderSubtitle.Text = "Update the supplier details. Purchase and payment history is preserved.";
        SaveButton.Content = "Save changes";

        NameInput.Text = s.Name;
        CodeInput.Text = s.Code;
        ContactInput.Text = s.ContactPerson ?? string.Empty;
        PhoneInput.Text = s.Phone ?? string.Empty;
        EmailInput.Text = s.Email ?? string.Empty;
        AddressInput.Text = s.Address ?? string.Empty;
        BizRegInput.Text = s.BusinessRegNo ?? string.Empty;
        TaxRegInput.Text = s.TaxRegNo ?? string.Empty;
        NotesInput.Text = s.Notes ?? string.Empty;
        ActiveInput.IsChecked = s.IsActive;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var name = NameInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)) { ShowError("A supplier name is required."); return; }

        var email = string.IsNullOrWhiteSpace(EmailInput.Text) ? null : EmailInput.Text.Trim();
        if (email is not null && !IsLikelyEmail(email)) { ShowError("Enter a valid email address."); return; }

        string? Code() => string.IsNullOrWhiteSpace(CodeInput.Text) ? null : CodeInput.Text.Trim();
        string? Val(TextBox box) => string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim();

        if (_editing is null)
        {
            CreateResult = new CreateSupplierRequest
            {
                Name = name,
                Code = Code(),
                ContactPerson = Val(ContactInput),
                Phone = Val(PhoneInput),
                Email = email,
                Address = Val(AddressInput),
                BusinessRegNo = Val(BizRegInput),
                TaxRegNo = Val(TaxRegInput),
                Notes = Val(NotesInput),
                IsActive = ActiveInput.IsChecked == true
            };
        }
        else
        {
            UpdateResult = new UpdateSupplierRequest
            {
                Id = _editing.Id,
                Name = name,
                Code = Code(),
                ContactPerson = Val(ContactInput),
                Phone = Val(PhoneInput),
                Email = email,
                Address = Val(AddressInput),
                BusinessRegNo = Val(BizRegInput),
                TaxRegNo = Val(TaxRegInput),
                Notes = Val(NotesInput),
                IsActive = ActiveInput.IsChecked == true
            };
        }

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static bool IsLikelyEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0 || at >= email.Length - 1) return false;
        var dot = email.IndexOf('.', at);
        return dot > at + 1 && dot < email.Length - 1 && !email.Contains(' ');
    }
}