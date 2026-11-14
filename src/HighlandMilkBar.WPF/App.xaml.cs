using System.IO;
using System.Windows;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using HighlandMilkBar.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.WPF;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HighlandMilkBar");
            Directory.CreateDirectory(appData);
            var databasePath = Path.Combine(appData, "highland-milkbar.db");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            var db = new AppDbContext(options);
            await DbInitializer.InitializeAsync(db);

            var productService = new ProductService(db);
            var dashboardService = new DashboardService(db);
            var mainViewModel = new MainViewModel(dashboardService, productService);
            await mainViewModel.InitializeAsync();

            var mainWindow = new MainWindow { DataContext = mainViewModel };
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The application could not start.\n\nDetails: {ex.Message}",
                "Highland Milk Bar - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
