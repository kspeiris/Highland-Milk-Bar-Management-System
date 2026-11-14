using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

public sealed class DashboardService(AppDbContext db) : IDashboardService
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var start = DateTime.Today;
        var end = start.AddDays(1);

        // SQLite does not implement every decimal aggregate operation server-side.
        // Load today's totals and sum with .NET decimal arithmetic for exact currency handling.
        var saleAmounts = await db.Sales.AsNoTracking()
            .Where(s => s.SoldAt >= start && s.SoldAt < end && s.Status == SaleStatus.Completed)
            .Select(s => s.TotalAmount)
            .ToListAsync(cancellationToken);
        var revenue = saleAmounts.Sum();

        var transactions = await db.Sales
            .CountAsync(s => s.SoldAt >= start && s.SoldAt < end && s.Status == SaleStatus.Completed, cancellationToken);

        var activeProducts = await db.Products.CountAsync(p => p.IsActive, cancellationToken);
        // Compare decimal quantities in .NET rather than relying on SQLite decimal comparison.
        var trackedQuantities = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.TrackInventory)
            .Select(p => new { p.CurrentStock, p.ReorderLevel })
            .ToListAsync(cancellationToken);
        var lowStock = trackedQuantities.Count(p => p.CurrentStock <= p.ReorderLevel);

        return new DashboardSnapshot
        {
            RevenueToday = revenue,
            TransactionsToday = transactions,
            ActiveProducts = activeProducts,
            LowStockItems = lowStock
        };
    }
}
