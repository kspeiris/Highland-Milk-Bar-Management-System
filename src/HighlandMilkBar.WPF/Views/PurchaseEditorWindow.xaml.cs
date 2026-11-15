using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class PurchaseEditorWindow : Window
{
    private readonly PurchaseEditorViewModel _viewModel;

    public PurchaseDetailDto? Result { get; private set; }

    public PurchaseEditorWindow(PurchaseEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.Completed += OnCompleted;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void OnCompleted(PurchaseDetailDto detail)
    {
        Result = detail;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}