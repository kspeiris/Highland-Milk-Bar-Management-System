using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.WPF.Views;

public partial class CustomerEditorWindow : Window
{
    private readonly CustomerProfileDto? _editing;

    public CustomerCreateRequest? CreateResult { get; private set; }
    public CustomerUpdateRequest? UpdateResult { get; private set; }

    public CustomerEditorWindow()
    {
        InitializeComponent();
        _editing = null;
        SetAddMode();
    }

    public CustomerEditorWindow(CustomerProfileDto customer)
    {
        InitializeComponent();
        _editing = customer;
        SetEditMode(customer);
    }

    private void SetAddMode()
    {
        Title = "Add customer";
        HeaderTitle.Text = "Add a customer";
        HeaderSubtitle.Text = "Enter the customer details. You can update these values later.";
        SaveButton.Content = "Save customer";
    }

    private void SetEditMode(CustomerProfileDto c)
    {
        Title = "Edit customer";
        HeaderTitle.Text = "Edit customer";
        HeaderSubtitle.Text = "Update the customer details. Purchase history is preserved.";
        SaveButton.Content = "Save changes";

        CodeInput.Text = c.CustomerCode;
        TypeInput.SelectedIndex = c.Type == CustomerType.Business ? 1 : 0;
        NameInput.Text = c.Name;
        PhoneInput.Text = c.Phone ?? string.Empty;
        AltPhoneInput.Text = c.AlternativePhone ?? string.Empty;
        EmailInput.Text = c.Email ?? string.Empty;
        CityInput.Text = c.City ?? string.Empty;
        DobInput.SelectedDate = c.DateOfBirth;
        AddressInput.Text = c.Address ?? string.Empty;
        NotesInput.Text = c.Notes ?? string.Empty;
        ActiveInput.IsChecked = c.IsActive;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var name = NameInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)) { ShowError("A customer name is required."); return; }

        var phone = string.IsNullOrWhiteSpace(PhoneInput.Text) ? null : PhoneInput.Text.Trim();
        if (_editing is null && phone is null) { ShowError("A primary phone number is required."); return; }

        var email = string.IsNullOrWhiteSpace(EmailInput.Text) ? null : EmailInput.Text.Trim();
        if (email is not null && !IsLikelyEmail(email)) { ShowError("Enter a valid email address."); return; }

        var type = TypeInput.SelectedIndex == 1 ? CustomerType.Business : CustomerType.Individual;
        string? Val(System.Windows.Controls.TextBox box) => string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim();

        if (_editing is null)
        {
            CreateResult = new CustomerCreateRequest
            {
                Type = type,
                Name = name,
                Phone = phone,
                AlternativePhone = Val(AltPhoneInput),
                Email = email,
                Address = Val(AddressInput),
                City = Val(CityInput),
                DateOfBirth = DobInput.SelectedDate,
                Notes = Val(NotesInput),
                IsActive = ActiveInput.IsChecked == true
            };
        }
        else
        {
            UpdateResult = new CustomerUpdateRequest
            {
                Id = _editing.Id,
                Type = type,
                Name = name,
                Phone = phone,
                AlternativePhone = Val(AltPhoneInput),
                Email = email,
                Address = Val(AddressInput),
                City = Val(CityInput),
                DateOfBirth = DobInput.SelectedDate,
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