using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.DTOs.Reports;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services.Reports;

/// <summary>
/// Builds report datasets by reusing the existing domain query services. Financial reports
/// come straight from <see cref="IFinancialQueryService"/> so they match the Finance workspace.
/// Read-only: no ledger is modified.
/// </summary>
public sealed class ReportQueryService(
    AppDbContext db,
    IBusinessAnalyticsQueryService analytics,
    IFinancialQueryService financial,
    IInventoryQueryService inventory,
    ICustomerQueryService customers,
    IPurchaseService purchases,
    IExpenseQueryService expenses,
    IMilkBarService milkBar) : IReportQueryService
{
    public async Task<ReportResult> GenerateAsync(string reportKey, ReportingPeriod period, CancellationToken cancellationToken = default)
    {
        return reportKey switch
        {
            "sales.summary" => await SalesSummaryAsync(period, cancellationToken),
            "sales.transactions" => await SalesTransactionsAsync(period, cancellationToken),
            "sales.by_product" => await SalesByProductAsync(period, cancellationToken),
            "sales.by_category" => await SalesByCategoryAsync(period, cancellationToken),
            "sales.by_payment" => await SalesByPaymentAsync(period, cancellationToken),
            "products.performance" => await ProductPerformanceAsync(period, cancellationToken),
            "products.no_sale" => await ProductNoSaleAsync(period, cancellationToken),
            "inventory.stock" => await InventoryStockAsync(cancellationToken),
            "inventory.valuation" => await InventoryValuationAsync(cancellationToken),
            "inventory.low_stock" => await InventoryLowStockAsync(cancellationToken),
            "inventory.expiry" => await InventoryExpiryAsync(cancellationToken),
            "purchases.outstanding" => await PurchasesOutstandingAsync(cancellationToken),
            "purchases.by_supplier" => await PurchasesBySupplierAsync(cancellationToken),
            "customers.directory" => await CustomersDirectoryAsync(cancellationToken),
            "customers.top_spending" => await CustomersTopSpendingAsync(cancellationToken),
            "expenses.by_category" => await ExpensesByCategoryAsync(period, cancellationToken),
            "financial.pl" => await ProfitLossAsync(period, cancellationToken),
            "financial.cashflow" => await CashFlowAsync(period, cancellationToken),
            "financial.payables" => await FinancialPayablesAsync(cancellationToken),
            "financial.payment_analysis" => await PaymentAnalysisAsync(period, cancellationToken),
            "milkbar.recipe_costing" => await RecipeCostingAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unknown report '{reportKey}'.")
        };
    }

    // ---------------- Sales ----------------

    private async Task<ReportResult> SalesSummaryAsync(ReportingPeriod p, CancellationToken ct)
    {
        var s = await analytics.GetSalesAnalyticsAsync(p, ct);
        return Build("sales.summary", "Sales Summary", ReportCategory.Sales, p,
        [
            Metric("Gross sales", Money(s.GrossSales)),
            Metric("Net sales", Money(s.NetSales)),
            Metric("Orders", s.Orders.ToString()),
            Metric("Average order value", Money(s.AverageOrderValue)),
            Metric("Units sold", s.UnitsSold.ToString("N0")),
            Metric("Best day", s.BestDay ?? "—")
        ],
        [
            Col("Period", ReportCellType.Text),
            Col("Net sales", ReportCellType.Currency),
            Col("Orders", ReportCellType.Integer)
        ],
        s.Trend.Select(t => Row(t.Label, t.NetSales, t.Orders)));
    }

    private async Task<ReportResult> SalesTransactionsAsync(ReportingPeriod p, CancellationToken ct)
    {
        var rows = await db.Sales.AsNoTracking()
            .Where(x => x.SoldAt >= p.Start && x.SoldAt < p.EndExclusive && x.Status != SaleStatus.Voided)
            .OrderByDescending(x => x.SoldAt)
            .Select(x => new { x.ReceiptNumber, x.SoldAt, CustomerName = x.Customer != null ? x.Customer.Name : string.Empty, x.Subtotal, x.DiscountAmount, x.TotalAmount, x.Status })
            .ToListAsync(ct);

        return Build("sales.transactions", "Sales Transactions", ReportCategory.Sales, p,
            [Metric("Transactions", rows.Count.ToString())],
        [
            Col("Invoice", ReportCellType.Text),
            Col("Date", ReportCellType.Date),
            Col("Customer", ReportCellType.Text),
            Col("Gross", ReportCellType.Currency),
            Col("Discount", ReportCellType.Currency),
            Col("Net", ReportCellType.Currency),
            Col("Status", ReportCellType.Text)
        ],
        rows.Select(x => Row(x.ReceiptNumber, x.SoldAt, string.IsNullOrWhiteSpace(x.CustomerName) ? "Walk-in" : x.CustomerName, x.Subtotal, x.DiscountAmount, x.TotalAmount, x.Status.ToString())));
    }

    private async Task<ReportResult> SalesByProductAsync(ReportingPeriod p, CancellationToken ct)
    {
        var products = await analytics.GetProductAnalyticsAsync(p, ct);
        return Build("sales.by_product", "Sales by Product", ReportCategory.Sales, p,
            [Metric("Products sold", products.TopSellers.Count.ToString())],
        [
            Col("Product", ReportCellType.Text),
            Col("SKU", ReportCellType.Text),
            Col("Category", ReportCellType.Text),
            Col("Units", ReportCellType.Number),
            Col("Revenue", ReportCellType.Currency),
            Col("Gross profit", ReportCellType.Currency)
        ],
        products.TopSellers.Select(x => Row(x.Name, x.Sku, x.CategoryName, x.UnitsSold, x.Revenue, x.GrossProfit)));
    }

    private async Task<ReportResult> SalesByCategoryAsync(ReportingPeriod p, CancellationToken ct)
    {
        var s = await analytics.GetSalesAnalyticsAsync(p, ct);
        return Build("sales.by_category", "Sales by Category", ReportCategory.Sales, p,
            [Metric("Categories", s.ByCategory.Count.ToString())],
        [
            Col("Category", ReportCellType.Text),
            Col("Revenue", ReportCellType.Currency),
            Col("Units", ReportCellType.Number),
            Col("Share", ReportCellType.Percent)
        ],
        s.ByCategory.Select(x => Row(x.CategoryName, x.Amount, x.Units, x.Percentage)));
    }

    private async Task<ReportResult> SalesByPaymentAsync(ReportingPeriod p, CancellationToken ct)
    {
        var s = await analytics.GetSalesAnalyticsAsync(p, ct);
        return Build("sales.by_payment", "Payment Method Report", ReportCategory.Sales, p,
            [Metric("Methods", s.ByPayment.Count.ToString())],
        [
            Col("Method", ReportCellType.Text),
            Col("Amount", ReportCellType.Currency),
            Col("Transactions", ReportCellType.Integer),
            Col("Share", ReportCellType.Percent)
        ],
        s.ByPayment.Select(x => Row(x.MethodText, x.TotalAmount, x.TransactionCount, x.Percentage)));
    }

    // ---------------- Products ----------------

    private async Task<ReportResult> ProductPerformanceAsync(ReportingPeriod p, CancellationToken ct)
    {
        var products = await analytics.GetProductAnalyticsAsync(p, ct);
        return Build("products.performance", "Product Performance", ReportCategory.Products, p,
            [Metric("Ranked products", products.TopSellers.Count.ToString())],
        [
            Col("Product", ReportCellType.Text),
            Col("Category", ReportCellType.Text),
            Col("Units", ReportCellType.Number),
            Col("Revenue", ReportCellType.Currency),
            Col("Gross profit", ReportCellType.Currency)
        ],
        products.TopSellers.Select(x => Row(x.Name, x.CategoryName, x.UnitsSold, x.Revenue, x.GrossProfit)));
    }

    private async Task<ReportResult> ProductNoSaleAsync(ReportingPeriod p, CancellationToken ct)
    {
        var products = await analytics.GetProductAnalyticsAsync(p, ct);
        return Build("products.no_sale", "Products With No Sales", ReportCategory.Products, p,
            [Metric("Products", products.NoSale.Count.ToString())],
        [
            Col("Product", ReportCellType.Text),
            Col("SKU", ReportCellType.Text),
            Col("Category", ReportCellType.Text)
        ],
        products.NoSale.Select(x => Row(x.Name, x.Sku, x.CategoryName)));
    }

    // ---------------- Inventory ----------------

    private async Task<ReportResult> InventoryStockAsync(CancellationToken ct)
    {
        var page = await inventory.GetStockAsync(new StockFilter { Page = 1, PageSize = 2000 }, ct);
        return Build("inventory.stock", "Current Stock", ReportCategory.Inventory, SnapshotPeriod(),
            [Metric("Products", page.TotalCount.ToString())],
        [
            Col("Product", ReportCellType.Text),
            Col("SKU", ReportCellType.Text),
            Col("Category", ReportCellType.Text),
            Col("Physical", ReportCellType.Number),
            Col("Sellable", ReportCellType.Number),
            Col("Status", ReportCellType.Text)
        ],
        page.Items.Select(x => Row(x.Name, x.Sku, x.CategoryName, x.PhysicalStock, x.SellableStock, x.StatusText)));
    }

    private async Task<ReportResult> InventoryValuationAsync(CancellationToken ct)
    {
        var o = await inventory.GetOverviewAsync(ct);
        return Build("inventory.valuation", "Inventory Valuation", ReportCategory.Inventory, SnapshotPeriod(),
        [
            Metric("Physical value", Money(o.PhysicalStockValue)),
            Metric("Sellable value", Money(o.SellableStockValue)),
            Metric("Tracked products", o.TrackedProducts.ToString())
        ],
        [
            Col("Category", ReportCellType.Text),
            Col("Quantity", ReportCellType.Number),
            Col("Value", ReportCellType.Currency)
        ],
        o.StockByCategory.Select(x => Row(x.CategoryName, x.Quantity, x.Value)));
    }

    private async Task<ReportResult> InventoryLowStockAsync(CancellationToken ct)
    {
        var rows = await inventory.GetLowStockAsync(ct);
        return Build("inventory.low_stock", "Low Stock", ReportCategory.Inventory, SnapshotPeriod(),
            [Metric("Products", rows.Count.ToString())],
        [
            Col("Product", ReportCellType.Text),
            Col("SKU", ReportCellType.Text),
            Col("Category", ReportCellType.Text),
            Col("Sellable", ReportCellType.Number),
            Col("Reorder level", ReportCellType.Number),
            Col("Shortfall", ReportCellType.Number)
        ],
        rows.Select(x => Row(x.Name, x.Sku, x.CategoryName, x.SellableStock, x.ReorderLevel, x.Shortfall)));
    }

    private async Task<ReportResult> InventoryExpiryAsync(CancellationToken ct)
    {
        var rows = await inventory.GetExpiredAsync(ct);
        return Build("inventory.expiry", "Expiry Report", ReportCategory.Inventory, SnapshotPeriod(),
            [Metric("Expired batches", rows.Count.ToString())],
        [
            Col("Product", ReportCellType.Text),
            Col("Batch", ReportCellType.Text),
            Col("Expiry", ReportCellType.Date),
            Col("Quantity", ReportCellType.Number),
            Col("Value", ReportCellType.Currency)
        ],
        rows.Select(x => Row(x.ProductName, x.BatchNumber, x.ExpiryDate, x.QuantityRemaining, x.Value)));
    }

    // ---------------- Purchases ----------------

    private async Task<ReportResult> PurchasesOutstandingAsync(CancellationToken ct)
    {
        var liab = await financial.GetSupplierLiabilitiesAsync(ct);
        return Build("purchases.outstanding", "Supplier Outstanding", ReportCategory.Purchases, SnapshotPeriod(),
        [
            Metric("Outstanding", Money(liab.TotalOutstanding)),
            Metric("Overdue", Money(liab.OverdueTotal)),
            Metric("Suppliers", liab.SupplierCount.ToString())
        ],
        [
            Col("Supplier", ReportCellType.Text),
            Col("Received", ReportCellType.Currency),
            Col("Paid", ReportCellType.Currency),
            Col("Outstanding", ReportCellType.Currency),
            Col("Overdue", ReportCellType.Currency),
            Col("Ageing", ReportCellType.Text)
        ],
        liab.Lines.Select(x => Row(x.SupplierName, x.ReceivedValue, x.AmountPaid, x.Outstanding, x.OverdueAmount, x.AgeingBucket)));
    }

    private async Task<ReportResult> PurchasesBySupplierAsync(CancellationToken ct)
    {
        var d = await purchases.GetDashboardAsync(ct);
        return Build("purchases.by_supplier", "Purchases by Supplier", ReportCategory.Purchases, SnapshotPeriod(),
        [
            Metric("Purchase value", Money(d.PurchaseValue)),
            Metric("Received purchases", d.TotalReceivedPurchases.ToString())
        ],
        [
            Col("Supplier", ReportCellType.Text),
            Col("Purchases", ReportCellType.Integer),
            Col("Value", ReportCellType.Currency)
        ],
        d.BySupplier.Select(x => Row(x.SupplierName, x.Purchases, x.Value)));
    }

    // ---------------- Customers ----------------

    private async Task<ReportResult> CustomersDirectoryAsync(CancellationToken ct)
    {
        var page = await customers.SearchAsync(new CustomerSearchFilter { Page = 1, PageSize = 2000 }, ct);
        return Build("customers.directory", "Customer Directory", ReportCategory.Customers, SnapshotPeriod(),
            [Metric("Customers", page.TotalCount.ToString())],
        [
            Col("Code", ReportCellType.Text),
            Col("Name", ReportCellType.Text),
            Col("Type", ReportCellType.Text),
            Col("Phone", ReportCellType.Text),
            Col("Purchases", ReportCellType.Integer),
            Col("Lifetime spend", ReportCellType.Currency),
            Col("Last purchase", ReportCellType.Date),
            Col("Status", ReportCellType.Text)
        ],
        page.Items.Select(x => Row(x.CustomerCode, x.Name, x.TypeText, x.Phone, x.TotalPurchases, x.TotalSpending, x.LastPurchase, x.StatusText)));
    }

    private async Task<ReportResult> CustomersTopSpendingAsync(CancellationToken ct)
    {
        var a = await customers.GetAnalyticsAsync(ct);
        return Build("customers.top_spending", "Top Customers by Spending", ReportCategory.Customers, SnapshotPeriod(),
            [Metric("Customers", a.TotalCustomers.ToString())],
        [
            Col("Customer", ReportCellType.Text),
            Col("Purchases", ReportCellType.Integer),
            Col("Spending", ReportCellType.Currency),
            Col("Last purchase", ReportCellType.Date)
        ],
        a.TopCustomersBySpending.Select(x => Row(x.Name, x.TotalPurchases, x.TotalSpending, x.LastPurchase)));
    }

    // ---------------- Expenses ----------------

    private async Task<ReportResult> ExpensesByCategoryAsync(ReportingPeriod p, CancellationToken ct)
    {
        var r = await expenses.GetReportAsync(p.Start, p.EndExclusive.AddDays(-1), ct);
        return Build("expenses.by_category", "Expenses by Category", ReportCategory.Expenses, p,
        [
            Metric("Total expenses", Money(r.TotalAmount)),
            Metric("Count", r.ExpenseCount.ToString()),
            Metric("Average", Money(r.AverageAmount))
        ],
        [
            Col("Category", ReportCellType.Text),
            Col("Amount", ReportCellType.Currency),
            Col("Count", ReportCellType.Integer),
            Col("Share", ReportCellType.Percent)
        ],
        r.ByCategory.Select(x => Row(x.CategoryName, x.TotalAmount, x.ExpenseCount, x.Percentage)));
    }

    // ---------------- Financial (STEP 11) ----------------

    private async Task<ReportResult> ProfitLossAsync(ReportingPeriod p, CancellationToken ct)
    {
        var s = await financial.GetSummaryAsync(p.Start, p.EndExclusive, ct);
        return Build("financial.pl", "Profit & Loss", ReportCategory.Financial, p,
        [
            Metric("Net sales", Money(s.NetSales)),
            Metric("Gross profit", Money(s.GrossProfit)),
            Metric("Operating profit", Money(s.OperatingProfit))
        ],
        [
            Col("Line", ReportCellType.Text),
            Col("Amount", ReportCellType.Currency)
        ],
        new[]
        {
            Row("Gross sales", s.GrossSales),
            Row("Less: discounts", -s.SalesDiscounts),
            Row("Less: sales returns", -s.SalesReturns),
            Row("Net sales", s.NetSales),
            Row("Less: cost of goods sold", -s.CostOfGoodsSold),
            Row("Gross profit", s.GrossProfit),
            Row("Less: operating expenses", -s.OperatingExpenses),
            Row("Operating profit", s.OperatingProfit)
        });
    }

    private async Task<ReportResult> CashFlowAsync(ReportingPeriod p, CancellationToken ct)
    {
        var c = await financial.GetCashFlowAsync(p.Start, p.EndExclusive, ct);
        var rows = new List<ReportRow>();
        foreach (var line in c.Inflows) rows.Add(Row(line.Label, line.Amount));
        rows.Add(Row("Total cash in", c.CashInflows));
        foreach (var line in c.Outflows) rows.Add(Row(line.Label, -line.Amount));
        rows.Add(Row("Total cash out", -c.CashOutflows));
        rows.Add(Row("Net cash flow", c.NetCashFlow));

        return Build("financial.cashflow", "Cash Flow", ReportCategory.Financial, p,
        [
            Metric("Cash in", Money(c.CashInflows)),
            Metric("Cash out", Money(c.CashOutflows)),
            Metric("Net cash flow", Money(c.NetCashFlow))
        ],
        [Col("Line", ReportCellType.Text), Col("Amount", ReportCellType.Currency)],
        rows);
    }

    private async Task<ReportResult> FinancialPayablesAsync(CancellationToken ct)
    {
        var liab = await financial.GetSupplierLiabilitiesAsync(ct);
        return Build("financial.payables", "Supplier Payables", ReportCategory.Financial, SnapshotPeriod(),
        [
            Metric("Total outstanding", Money(liab.TotalOutstanding)),
            Metric("Overdue", Money(liab.OverdueTotal)),
            Metric("Suppliers", liab.SupplierCount.ToString())
        ],
        [
            Col("Supplier", ReportCellType.Text),
            Col("Outstanding", ReportCellType.Currency),
            Col("Overdue", ReportCellType.Currency),
            Col("Ageing", ReportCellType.Text)
        ],
        liab.Lines.Select(x => Row(x.SupplierName, x.Outstanding, x.OverdueAmount, x.AgeingBucket)));
    }

    private async Task<ReportResult> PaymentAnalysisAsync(ReportingPeriod p, CancellationToken ct)
    {
        var rows = await financial.GetSalesPaymentBreakdownAsync(p.Start, p.EndExclusive, ct);
        return Build("financial.payment_analysis", "Payment Analysis", ReportCategory.Financial, p,
            [Metric("Methods", rows.Count.ToString())],
        [
            Col("Method", ReportCellType.Text),
            Col("Amount", ReportCellType.Currency),
            Col("Transactions", ReportCellType.Integer),
            Col("Share", ReportCellType.Percent)
        ],
        rows.Select(x => Row(x.MethodText, x.TotalAmount, x.TransactionCount, x.Percentage)));
    }

    // ---------------- Milk Bar ----------------

    private async Task<ReportResult> RecipeCostingAsync(CancellationToken ct)
    {
        var o = await milkBar.GetOverviewAsync(ct);
        return Build("milkbar.recipe_costing", "Recipe Costing (Estimated)", ReportCategory.MilkBar, SnapshotPeriod(),
        [
            Metric("Menu items", o.TotalMenuItems.ToString()),
            Metric("Average margin", o.AverageMarginPercent.ToString("N1") + "%")
        ],
        [
            Col("Menu item", ReportCellType.Text),
            Col("Selling price", ReportCellType.Currency),
            Col("Estimated cost", ReportCellType.Currency),
            Col("Margin %", ReportCellType.Number)
        ],
        o.HighestMarginProducts.Select(x => Row(x.Name, x.SellingPrice, x.Cost, x.MarginPercent)));
    }

    // ---------------- Helpers ----------------

    private static ReportingPeriod SnapshotPeriod()
        => ReportingPeriod.Custom(DateTime.Today, DateTime.Today.AddDays(1));

    private static ReportResult Build(string key, string title, ReportCategory category, ReportingPeriod period,
        IReadOnlyList<ReportSummaryItem> summary, IReadOnlyList<ReportColumn> columns, IEnumerable<ReportRow> rows)
        => new()
        {
            Key = key,
            Title = title,
            Category = category.ToString(),
            PeriodLabel = period.Label,
            GeneratedAt = DateTime.Now,
            Summary = summary,
            Columns = columns,
            Rows = rows.ToList()
        };

    private static ReportSummaryItem Metric(string label, string value) => new() { Label = label, Value = value };
    private static ReportColumn Col(string header, ReportCellType type) => new() { Header = header, Type = type };
    private static ReportRow Row(params object?[] values) => new() { Values = values };
    private static string Money(decimal value) => $"LKR {value:N2}";
}