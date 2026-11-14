using System.Windows;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.WPF.Views;

public partial class CategoryEditorWindow : Window
{
    private readonly CategoryDto? _editing;

    public CreateCategoryRequest? CreateResult { get; private set; }
    public UpdateCategoryRequest? UpdateResult { get; private set; }

    public CategoryEditorWindow()
    {
        InitializeComponent();
    }

    public CategoryEditorWindow(CategoryDto category)
    {
        InitializeComponent();
        _editing = category;
        Title = "Edit category";
        HeaderTitle.Text = "Edit category";
        NameInput.Text = category.Name;
        DescriptionInput.Text = category.Description ?? string.Empty;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        var name = NameInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = "A category name is required.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var description = string.IsNullOrWhiteSpace(DescriptionInput.Text) ? null : DescriptionInput.Text.Trim();

        if (_editing is null)
            CreateResult = new CreateCategoryRequest { Name = name, Description = description };
        else
            UpdateResult = new UpdateCategoryRequest { Id = _editing.Id, Name = name, Description = description };

        DialogResult = true;
    }
}