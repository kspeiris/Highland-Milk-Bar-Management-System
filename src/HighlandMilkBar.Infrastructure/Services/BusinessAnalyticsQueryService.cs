using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Read-only business intelligence. Financial figures come from <see cref="IFinancialQueryService"/>;
/// inventory, customer, milk bar, purchase and expense summaries come from their existing query
/// services; only sales/product aggregates that no service exposes are queried here (database-side,
/// then summed in memory with <see cref="decimal"/> because SQLite lacks decimal aggregates).
/// No method writes to any ledger.
/// </summary>
public sealed class BusinessAnalyticsQueryService(
    AppDbContext db,
    IFinancialQueryService financial,
    IInventoryQueryService inventory,
    ICustomerQueryService customers,
    IMilkBarService milkBar,
    IPurchaseService purchases,
    IExpenseQueryService expenses) : IBusinessAnalyticsQueryService
{
    private const int RankLimit = 10;

    public async Task<BusinessOverviewDto> GetOverviewAsync(ReportingPeriod period, CancellationToken cancellationToken = default)
    {
        var summary = await financial.GetSummaryAsync(period.Start, period.EndExclusive, cancellationToken);
        var previousSummary = await financial.GetSummaryAsync(period.Previous().Start, period.Previous().EndExclusive, cancellationToken);
        var core = await LoadSalesCoreAsync(period, cancellationToken);
        var (orders, unitsSold) = ComputeOrdersAndUnits(core);

        var customerStats = await customers.GetAnalyticsAsync(cancellationToken);
        var inventoryOverview = await inventory.GetOverviewAsync(cancellationToken);
        var productAnalytics = BuildProductAnalytics(core, await LoadProductMapAsync(cancellationToken));
        var trend = BuildTrend(core, period);
        var categorySales = BuildCategorySales(core.Items);
        var alerts = await BuildAlertsAsync(period, summary, orders, previousSummary, inventoryOverview, customerStats, cancellationToken);

        return new BusinessOverviewDto
        {
            Start = period.Start,
            EndExclusive = period.EndExclusive,
            PeriodLabel = period.Label,
            Financial = summary,
            Orders = orders,
            UnitsSold = unitsSold,
            AverageOrderValue = orders == 0 ? 0m : summary.NetSales / orders,
            TotalCustomers = customerStats.TotalCustomers,
            NewCustomers = customerStats.NewThisMonth,
            RepeatCustomers = customerStats.RepeatCustomers,
            Inventory = inventoryOverview,
            NetSalesComparison = Compare(summary.NetSales, previousSummary.NetSales),
            GrossProfitComparison = Compare(summary.GrossProfit, previousSummary.GrossProfit),
            ExpensesComparison = Compare(summary.OperatingExpenses, previousSummary.OperatingExpenses),
            OrdersComparison = Compare(orders, await CountOrdersAsync(period.Previous(), cancellationToken)),
            TopProducts = productAnalytics.TopSellers,
            CategorySales = categorySales,
            Trend = trend,
            Alerts = alerts
        };
    }

    public async Task<SalesAnalyticsDto> GetSalesAnalyticsAsync(ReportingPeriod period, CancellationToken cancellationToken = default)
    {
        var summary = await financial.GetSummaryAsync(period.Start, period.EndExclusive, cancellationToken);
        var core = await LoadSalesCoreAsync(period, cancellationToken);
        var (orders, unitsSold) = ComputeOrdersAndUnits(core);

        var bestDay = core.Sales
            .GroupBy(s => s.When.Date)
            .Select(g => new { Day = g.Key, Net = g.Sum(s => s.Total) })
            .OrderByDescending(x => x.Net)
            .FirstOrDefault();

        return new SalesAnalyticsDto
        {
            Start = period.Start,
            EndExclusive = period.EndExclusive,
            GrossSales = summary.GrossSales,
            NetSales = summary.NetSales,
            Orders = orders,
            AverageOrderValue = orders == 0 ? 0m : summary.NetSales / orders,
            UnitsSold = unitsSold,
            BestDay = bestDay is { } b && b.Net > 0m ? b.Day.ToString("dd MMM yyyy") : null,
            Trend = BuildTrend(core, period),
            ByCategory = BuildCategorySales(core.Items),
            ByPayment = await financial.GetSalesPaymentBreakdownAsync(period.Start, period.EndExclusive, cancellationToken),
            ByWeekday = BuildWeekday(core.Sales)
        };
    }

    public async Task<ProductAnalyticsDto> GetProductAnalyticsAsync(ReportingPeriod period, CancellationToken cancellationToken = default)
    {
        var core = await LoadSalesCoreAsync(period, cancellationToken);
        return BuildProductAnalytics(core, await LoadProductMapAsync(cancellationToken));
    }

    public Task<InventoryOverviewDto> GetInventoryAnalyticsAsync(CancellationToken cancellationToken = default)
        => inventory.GetOverviewAsync(cancellationToken);

    public Task<CustomerAnalyticsDto> GetCustomerAnalyticsAsync(CancellationToken cancellationToken = default)
        => customers.GetAnalyticsAsync(cancellationToken);

    public Task<MilkBarOverviewDto> GetMilkBarAnalyticsAsync(CancellationToken cancellationToken = default)
        => milkBar.GetOverviewAsync(cancellationToken);

    public Task<PurchaseDashboardDto> GetPurchaseAnalyticsAsync(CancellationToken cancellationToken = default)
        => purchases.GetDashboardAsync(cancellationToken);

    public async Task<ExpenseReportDto> GetExpenseAnalyticsAsync(ReportingPeriod period, CancellationToken cancellationToken = default)
        => await expenses.GetReportAsync(period.Start, period.EndExclusive.AddDays(-1), cancellationToken);

    public async Task<IReadOnlyList<BusinessAlertDto>> GetBusinessAlertsAsync(ReportingPeriod period, CancellationToken cancellationToken = default)
    {
        var summary = await financial.GetSummaryAsync(period.Start, period.EndExclusive, cancellationToken);
        var previous = await financial.GetSummaryAsync(period.Previous().Start, period.Previous().EndExclusive, cancellationToken);
        var inventoryOverview = await inventory.GetOverviewAsync(cancellationToken);
        var customerStats = await customers.GetAnalyticsAsync(cancellationToken);
        var orders = await CountOrdersAsync(period, cancellationToken);
        return await BuildAlertsAsync(period, summary, orders, previous, inventoryOverview, customerStats, cancellationToken);
    }

    // ---------------- Sales core (shared DB load) ----------------

    private sealed record SaleAgg(DateTime When, decimal Total);
    private sealed record ItemAgg(int? ProductId, string Name, string Category, decimal Quantity, decimal LineTotal, decimal UnitCost);
    private sealed record ReturnAgg(DateTime When, decimal Total);
    private sealed record ProductInfo(int Id, string Name, string Sku, string Category, bool IsActive);
    private sealed record SalesCore(List<SaleAgg> Sales, List<ItemAgg> Items, List<ReturnAgg> Returns);

    private async Task<SalesCore> LoadSalesCoreAsync(ReportingPeriod period, CancellationToken cancellationToken)
    {
        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.SoldAt >= period.Start && s.SoldAt < period.EndExclusive && s.Status != SaleStatus.Voided)
            .Select(s => new { s.SoldAt, s.TotalAmount })
            .ToListAsync(cancellationToken);

        var items = await db.SaleItems.AsNoTracking()
            .Where(i => i.Sale!.SoldAt >= period.Start && i.Sale!.SoldAt < period.EndExclusive && i.Sale!.Status != SaleStatus.Voided)
            .Select(i => new
            {
                i.ProductId,
                Name = i.ItemNameSnapshot,
                Category = i.Product != null && i.Product.Category != null ? i.Product.Category.Name : "Uncategorized",
                i.Quantity,
                i.LineTotal,
                i.UnitCostSnapshot
            })
            .ToListAsync(cancellationToken);

        var returns = await db.SalesReturns.AsNoTracking()
            .Where(r => r.ReturnedAt >= period.Start && r.ReturnedAt < period.EndExclusive && r.Status == SalesReturnStatus.Completed)
            .Select(r => new { r.ReturnedAt, r.TotalAmount })
            .ToListAsync(cancellationToken);

        return new SalesCore(
            sales.Select(s => new SaleAgg(s.SoldAt, s.TotalAmount)).ToList(),
            items.Select(i => new ItemAgg(i.ProductId, i.Name, i.Category, i.Quantity, i.LineTotal, i.UnitCostSnapshot)).ToList(),
            returns.Select(r => new ReturnAgg(r.ReturnedAt, r.TotalAmount)).ToList());
    }

    private static (int Orders, decimal Units) ComputeOrdersAndUnits(SalesCore core)
        => (core.Sales.Count, core.Items.Sum(i => i.Quantity));

    private async Task<int> CountOrdersAsync(ReportingPeriod period, CancellationToken cancellationToken)
        => await db.Sales.AsNoTracking()
            .CountAsync(s => s.SoldAt >= period.Start && s.SoldAt < period.EndExclusive && s.Status != SaleStatus.Voided, cancellationToken);

    private async Task<List<ProductInfo>> LoadProductMapAsync(CancellationToken cancellationToken)
        => await db.Products.AsNoTracking()
            .Select(p => new ProductInfo(
                p.Id, p.Name, p.Sku,
                p.Category != null ? p.Category.Name : string.Empty,
                p.IsActive))
            .ToListAsync(cancellationToken);

    // ---------------- Builders ----------------

    private static IReadOnlyList<SalesTrendPointDto> BuildTrend(SalesCore core, ReportingPeriod period)
    {
        var lengthDays = (int)(period.EndExclusive - period.Start).TotalDays;
        var result = new List<SalesTrendPointDto>();

        if (lengthDays <= 31)
        {
            for (var day = period.Start; day < period.EndExclusive; day = day.AddDays(1))
            {
                var net = core.Sales.Where(s => s.When.Date == day.Date).Sum(s => s.Total)
                          - core.Returns.Where(r => r.When.Date == day.Date).Sum(r => r.Total);
                var orders = core.Sales.Count(s => s.When.Date == day.Date);
                result.Add(new SalesTrendPointDto { Label = day.ToString("dd MMM"), NetSales = Round(net), Orders = orders });
            }
            return result;
        }

        for (var month = new DateTime(period.Start.Year, period.Start.Month, 1); month < period.EndExclusive; month = month.AddMonths(1))
        {
            var next = month.AddMonths(1);
            var net = core.Sales.Where(s => s.When >= month && s.When < next).Sum(s => s.Total)
                      - core.Returns.Where(r => r.When >= month && r.When < next).Sum(r => r.Total);
            var orders = core.Sales.Count(s => s.When >= month && s.When < next);
            result.Add(new SalesTrendPointDto { Label = month.ToString("MMM yyyy"), NetSales = Round(net), Orders = orders });
        }
        return result;
    }

    private static IReadOnlyList<CategorySalesDto> BuildCategorySales(List<ItemAgg> items)
    {
        var groups = items
            .GroupBy(i => i.Category)
            .Select(g => new { Name = g.Key, Amount = g.Sum(i => i.LineTotal), Units = g.Sum(i => i.Quantity) })
            .OrderByDescending(g => g.Amount)
            .ToList();
        var grand = groups.Sum(g => g.Amount);
        return groups.Select(g => new CategorySalesDto
        {
            CategoryName = g.Name,
            Amount = Round(g.Amount),
            Units = Round(g.Units),
            Percentage = grand == 0m ? 0m : Round(g.Amount / grand * 100m)
        }).ToList();
    }

    private static IReadOnlyList<WeekdaySalesDto> BuildWeekday(List<SaleAgg> sales)
    {
        var ordered = Enum.GetValues<DayOfWeek>();
        return ordered.Select(d => new
        {
            Day = d,
            Amount = sales.Where(s => s.When.DayOfWeek == d).Sum(s => s.Total),
            Orders = sales.Count(s => s.When.DayOfWeek == d)
        })
        .Select(x => new WeekdaySalesDto { Day = x.Day.ToString(), Amount = Round(x.Amount), Orders = x.Orders })
        .OrderByDescending(x => x.Amount)
        .ToList();
    }

    private static ProductAnalyticsDto BuildProductAnalytics(SalesCore core, List<ProductInfo> products)
    {
        var skuById = products.ToDictionary(p => p.Id, p => p);

        var grouped = core.Items
            .Where(i => i.ProductId != null)
            .GroupBy(i => i.ProductId!.Value)
            .Select(g =>
            {
                var first = g.First();
                skuById.TryGetValue(g.Key, out var info);
                var hasCost = g.Any(i => i.UnitCost > 0m);
                var profit = hasCost ? g.Sum(i => i.LineTotal - i.Quantity * i.UnitCost) : (decimal?)null;
                return new ProductPerformanceDto
                {
                    ProductId = g.Key,
                    Name = string.IsNullOrEmpty(first.Name) ? info?.Name ?? "Item" : first.Name,
                    Sku = info?.Sku ?? string.Empty,
                    CategoryName = string.IsNullOrEmpty(first.Category) ? info?.Category ?? string.Empty : first.Category,
                    UnitsSold = Round(g.Sum(i => i.Quantity)),
                    Revenue = Round(g.Sum(i => i.LineTotal)),
                    GrossProfit = profit.HasValue ? Round(profit.Value) : null
                };
            })
            .ToList();

        var topSellers = grouped.OrderByDescending(p => p.UnitsSold).Take(RankLimit).ToList();
        var highProfit = grouped.Where(p => p.GrossProfit is > 0m).OrderByDescending(p => p.GrossProfit).Take(RankLimit).ToList();
        var lowPerformers = grouped.Where(p => p.UnitsSold > 0m).OrderBy(p => p.UnitsSold).Take(RankLimit).ToList();

        var soldIds = grouped.Select(p => p.ProductId).ToHashSet();
        var noSale = products
            .Where(p => p.IsActive && !soldIds.Contains(p.Id))
            .OrderBy(p => p.Name)
            .Take(50)
            .Select(p => new ProductPerformanceDto
            {
                ProductId = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryName = p.Category,
                UnitsSold = 0m,
                Revenue = 0m,
                GrossProfit = null
            })
            .ToList();

        return new ProductAnalyticsDto
        {
            TopSellers = topSellers,
            HighProfit = highProfit,
            LowPerformers = lowPerformers,
            NoSale = noSale
        };
    }

    // ---------------- Alerts ----------------

    private async Task<IReadOnlyList<BusinessAlertDto>> BuildAlertsAsync(
        ReportingPeriod period, FinancialSummaryDto summary, int orders, FinancialSummaryDto previous,
        InventoryOverviewDto inventory, CustomerAnalyticsDto customers, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var alerts = new List<BusinessAlertDto>();

        foreach (var a in inventory.LowStockAlerts)
            alerts.Add(new BusinessAlertDto
            {
                Type = "Inventory.LowStock",
                Title = "Low stock",
                Description = $"{a.ProductName} is below its reorder level ({a.Detail}).",
                Severity = BusinessAlertSeverity.Warning,
                Category = BusinessAlertCategory.Inventory,
                Metric = a.Detail,
                SuggestedAction = "Review stock requirements and create a replenishment purchase.",
                RelatedRecordId = null,
                NavigationTarget = "Inventory",
                GeneratedAt = now
            });

        foreach (var a in inventory.ExpiryAlerts)
            alerts.Add(new BusinessAlertDto
            {
                Type = "Inventory.Expiry",
                Title = "Expiry",
                Description = $"{a.ProductName}: {a.Detail}.",
                Severity = BusinessAlertSeverity.Warning,
                Category = BusinessAlertCategory.Inventory,
                Metric = a.Detail,
                SuggestedAction = "Prioritise selling or removing the affected batches (FEFO).",
                RelatedRecordId = null,
                NavigationTarget = "Inventory",
                GeneratedAt = now
            });

        var milkBarOverview = await milkBar.GetOverviewAsync(cancellationToken);
        foreach (var a in milkBarOverview.Alerts)
            alerts.Add(new BusinessAlertDto
            {
                Type = $"MilkBar.{a.Type}",
                Title = "Milk Bar",
                Description = a.Message,
                Severity = a.Severity switch
                {
                    MilkBarAlertSeverity.Critical => BusinessAlertSeverity.Critical,
                    MilkBarAlertSeverity.Warning => BusinessAlertSeverity.Warning,
                    _ => BusinessAlertSeverity.Information
                },
                Category = BusinessAlertCategory.Recipe,
                Metric = string.Empty,
                SuggestedAction = "Review the recipe and ingredient availability in the Milk Bar workspace.",
                RelatedRecordId = null,
                NavigationTarget = "Milk Bar",
                GeneratedAt = now
            });

        if (previous.NetSales > 0m && summary.NetSales < previous.NetSales * 0.7m)
            alerts.Add(new BusinessAlertDto
            {
                Type = "Sales.Decline",
                Title = "Sales decline",
                Description = $"Net sales are down {Round((1 - summary.NetSales / previous.NetSales) * 100m):N0}% versus the previous period.",
                Severity = BusinessAlertSeverity.Warning,
                Category = BusinessAlertCategory.Sales,
                Metric = $"LKR {summary.NetSales:N2} vs LKR {previous.NetSales:N2}",
                SuggestedAction = "Review recent sales, pricing and promotions for the period.",
                RelatedRecordId = null,
                NavigationTarget = "Sales Analytics",
                GeneratedAt = now
            });

        if (previous.OperatingExpenses > 0m && summary.OperatingExpenses > previous.OperatingExpenses * 1.2m)
            alerts.Add(new BusinessAlertDto
            {
                Type = "Expense.Increase",
                Title = "Expense increase",
                Description = $"Operating expenses rose {Round((summary.OperatingExpenses / previous.OperatingExpenses - 1) * 100m):N0}% versus the previous period.",
                Severity = BusinessAlertSeverity.Warning,
                Category = BusinessAlertCategory.Expense,
                Metric = $"LKR {summary.OperatingExpenses:N2} vs LKR {previous.OperatingExpenses:N2}",
                SuggestedAction = "Review expense categories for unexpected increases.",
                RelatedRecordId = null,
                NavigationTarget = "Expenses",
                GeneratedAt = now
            });

        if (summary.SupplierOutstanding > 0m)
            alerts.Add(new BusinessAlertDto
            {
                Type = "Finance.Payables",
                Title = "Supplier payables",
                Description = $"LKR {summary.SupplierOutstanding:N2} is outstanding to suppliers.",
                Severity = BusinessAlertSeverity.Information,
                Category = BusinessAlertCategory.Finance,
                Metric = $"LKR {summary.SupplierOutstanding:N2}",
                SuggestedAction = "Review supplier balances and schedule payments.",
                RelatedRecordId = null,
                NavigationTarget = "Finance",
                GeneratedAt = now
            });

        if (customers.RepeatCustomers == 0 && customers.TotalCustomers > 0)
            alerts.Add(new BusinessAlertDto
            {
                Type = "Customer.NoRepeat",
                Title = "No repeat customers",
                Description = "No repeat customers were recorded in the current period.",
                Severity = BusinessAlertSeverity.Information,
                Category = BusinessAlertCategory.Customer,
                Metric = customers.TotalCustomers.ToString(),
                SuggestedAction = "Consider loyalty or follow-up campaigns to encourage repeat visits.",
                RelatedRecordId = null,
                NavigationTarget = "Customers",
                GeneratedAt = now
            });

        return alerts
            .GroupBy(a => a.Key)
            .Select(g => g.First())
            .OrderByDescending(a => a.Severity)
            .ThenBy(a => a.Category)
            .ToList();
    }

    // ---------------- Helpers ----------------

    private static ComparisonDto Compare(decimal current, decimal previous)
    {
        var diff = current - previous;
        decimal? pct = previous == 0m ? null : Round(diff / Math.Abs(previous) * 100m);
        var isNew = previous == 0m && current != 0m;
        var text = isNew
            ? "New activity"
            : previous == 0m && current == 0m
                ? "No change"
                : pct is { } p
                    ? $"{(p >= 0 ? "+" : "")}{p:N1}% vs previous"
                    : "n/a";
        return new ComparisonDto
        {
            Current = Round(current),
            Previous = Round(previous),
            Difference = Round(diff),
            PercentChange = pct,
            IsNewActivity = isNew,
            IsIncrease = diff > 0m,
            ChangeText = text
        };
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}