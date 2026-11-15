using System.Windows;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.WPF.ViewModels;

namespace HighlandMilkBar.WPF.Views;

public partial class ExpensesView : System.Windows.Controls.UserControl
{
    private ExpensesViewModel? _viewModel;

    public ExpensesView() => InitializeComponent();

    private async void ExpensesView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null && DataContext is ExpensesViewModel vm)
        {
            _viewModel = vm;
            vm.NewRequested += OnNew;
            vm.EditRequested += OnEdit;
            vm.DetailsRequested += OnDetails;
            vm.AnalyticsRequested += OnAnalytics;
            vm.CategoriesRequested += OnCategories;
        }
        if (_viewModel is { } hub)
            await hub.LoadAsync();
    }

    private void OnNew()
    {
        if (_viewModel is not { } hub) return;
        var window = new ExpenseEditorWindow(hub.ExpenseService);
        if (window.ShowDialog() == true && window.CreateResult is { } request)
            _ = SaveNew(hub, request);
    }

    private async System.Threading.Tasks.Task SaveNew(ExpensesViewModel hub, ExpenseCreateRequest request)
    {
        try { await hub.ExpenseService.CreateAsync(request); await hub.LoadAsync(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Cannot save expense", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void OnEdit(ExpenseRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var detail = await hub.QueryService.GetByIdAsync(row.Id);
        if (detail is null) return;
        var window = new ExpenseEditorWindow(hub.ExpenseService, detail);
        if (window.ShowDialog() == true && window.UpdateResult is { } request)
        {
            try { await hub.ExpenseService.UpdateAsync(request); await hub.LoadAsync(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Cannot save expense", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }

    private async void OnDetails(ExpenseRowDto row)
    {
        if (_viewModel is not { } hub) return;
        var detail = await hub.QueryService.GetByIdAsync(row.Id);
        if (detail is null) return;
        var window = new ExpenseDetailsWindow(detail);
        window.ShowDialog();
    }

    private void OnAnalytics()
    {
        if (_viewModel is not { } hub) return;
        new ExpenseAnalyticsWindow(hub.QueryService).ShowDialog();
    }

    private void OnCategories()
    {
        if (_viewModel is not { } hub) return;
        var window = new ExpenseCategoriesWindow(hub.ExpenseService);
        window.ShowDialog();
        _ = hub.LoadAsync();
    }
}