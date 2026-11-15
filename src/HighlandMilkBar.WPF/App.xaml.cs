using System.IO;
using System.Windows;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using HighlandMilkBar.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.WPF;

public partial class App : System.Windows.Application
{
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
            var dashboardService = new DashboardService(db);
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
            var salesService = new SalesService(db, inventoryService, loyaltyService);
            var mainViewModel = new MainViewModel(
                dashboardService, productService, categoryService, inventoryService, inventoryQueryService,
                salesService, salesService, receiptService, milkBarService, recipeService,
                purchaseService, supplierService, supplierPaymentService,
                customerService, customerQueryService, loyaltyService);
            await mainViewModel.InitializeAsync();

            var mainWindow = new MainWindow { DataContext = mainViewModel };
            MainWindow = mainWindow;
            mainWindow.Show();
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
}
