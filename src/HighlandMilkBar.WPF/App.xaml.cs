using System.IO;
using System.Windows;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using HighlandMilkBar.Infrastructure.Services.Backup;
using HighlandMilkBar.Infrastructure.Services.Reports;
using HighlandMilkBar.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.WPF;

public partial class App : System.Windows.Application
{
    private BackupSchedulerService? _backupScheduler;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HighlandMilkBar");

        try
        {
            Directory.CreateDirectory(appData);
            var databasePath = Path.Combine(appData, "highland-milkbar.db");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            var db = new AppDbContext(options);
            await DbInitializer.InitializeAsync(db);

            var productService = new ProductService(db);
            var categoryService = new CategoryService(db);
            var inventoryService = new InventoryService(db);
            var inventoryQueryService = new InventoryQueryService(db);
            var receiptService = new ReceiptService(db);
            var recipeService = new RecipeService(db);
            var milkBarService = new MilkBarService(db, recipeService);
            var supplierService = new SupplierService(db);
            var supplierPaymentService = new SupplierPaymentService(db);
            var purchaseService = new PurchaseService(db, inventoryService);
            var loyaltyService = new LoyaltyService(db);
            var customerService = new CustomerService(db);
            var customerQueryService = new CustomerQueryService(db);
            var expenseService = new ExpenseService(db);
            var financialQueryService = new FinancialQueryService(db);
            var salesService = new SalesService(db, inventoryService, loyaltyService);
            var businessAnalytics = new BusinessAnalyticsQueryService(
                db, financialQueryService, inventoryQueryService, customerQueryService, milkBarService, purchaseService, expenseService);
            var reportCatalog = new ReportCatalogService();
            var reportQuery = new ReportQueryService(
                db, businessAnalytics, financialQueryService, inventoryQueryService, customerQueryService, purchaseService, expenseService, milkBarService);
            var reportExport = new ReportExportService();
            var backupService = new BackupService(db);
            var mainViewModel = new MainViewModel(
                businessAnalytics, productService, categoryService, inventoryService, inventoryQueryService,
                salesService, salesService, receiptService, milkBarService, recipeService,
                purchaseService, supplierService, supplierPaymentService,
                customerService, customerQueryService, loyaltyService, expenseService, expenseService,
                financialQueryService, reportCatalog, reportQuery, reportExport, backupService, backupService);
            await mainViewModel.InitializeAsync();

            var mainWindow = new MainWindow { DataContext = mainViewModel };
            MainWindow = mainWindow;
            mainWindow.Show();

            _backupScheduler = new BackupSchedulerService(backupService, backupService);
            _backupScheduler.Start();
        }
        catch (Exception ex)
        {
            try
            {
                Directory.CreateDirectory(appData);
                File.AppendAllText(
                    Path.Combine(appData, "startup.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Startup failed: {ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }

            MessageBox.Show(
                $"The application could not start.\n\nDetails: {ex.Message}",
                "Highland Milk Bar - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_backupScheduler is not null)
        {
            try { _backupScheduler.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch { }
        }

        base.OnExit(e);
    }
}
