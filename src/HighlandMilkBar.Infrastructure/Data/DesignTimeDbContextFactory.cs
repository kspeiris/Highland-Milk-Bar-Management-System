using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HighlandMilkBar.Infrastructure.Data;

/// <summary>
/// Enables <c>dotnet ef migrations</c> to construct the context without launching the
/// WPF application. The connection string is only used to select the provider; no
/// database file is created or touched at design time.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=highland-milkbar.db")
            .Options;

        return new AppDbContext(options);
    }
}