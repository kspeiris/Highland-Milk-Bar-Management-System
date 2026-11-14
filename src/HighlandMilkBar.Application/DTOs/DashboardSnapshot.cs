namespace HighlandMilkBar.Application.DTOs;

public sealed class DashboardSnapshot
{
    public decimal RevenueToday { get; init; }
    public int TransactionsToday { get; init; }
    public int ActiveProducts { get; init; }
    public int LowStockItems { get; init; }
}
