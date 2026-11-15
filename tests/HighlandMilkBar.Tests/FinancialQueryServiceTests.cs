using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

public sealed class FinancialQueryServiceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;
    private FinancialQueryService Service => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private static readonly DateTime Jan = new(2026, 1, 1);
    private static readonly DateTime Feb = new(2026, 2, 1);

    private Sale AddSaleFull(decimal subtotal, decimal discount, decimal cogs, DateTime soldAt,
        SaleStatus status = SaleStatus.Completed, int? cashSessionId = null, params (PaymentMethod Method, decimal Amount)[] payments)
    {
        var sale = new Sale
        {
            ReceiptNumber = $"RCP-{Guid.NewGuid():N}"[..12],
            Subtotal = subtotal,
            DiscountAmount = discount,
            TotalAmount = subtotal - discount,
            CostOfGoodsSold = cogs,
            AmountPaid = payments.Sum(p => p.Amount),
            Status = status,
            SoldAt = soldAt,
            CashSessionId = cashSessionId
        };
        sale.Items.Add(new SaleItem { ItemNameSnapshot = "Item", Quantity = 1, UnitPrice = subtotal - discount, LineTotal = subtotal - discount });
        foreach (var (method, amount) in payments)
            sale.Payments.Add(new Payment { Method = method, Amount = amount, PaidAt = soldAt });
        Db.Sales.Add(sale);
        Db.SaveChanges();
        return sale;
    }

    private void AddReturn(int saleId, decimal total, DateTime returnedAt, SalesReturnStatus status = SalesReturnStatus.Completed)
    {
        Db.SalesReturns.Add(new SalesReturn
        {
            ReturnNumber = $"RET-{Guid.NewGuid():N}"[..12],
            SaleId = saleId,
            ReturnedAt = returnedAt,
            TotalAmount = total,
            Status = status
        });
        Db.SaveChanges();
    }

    private void AddExpense(decimal amount, DateTime date, PaymentMethod method, int? cashSessionId = null)
    {
        Db.Expenses.Add(new Expense
        {
            Description = "Test expense",
            Amount = amount,
            ExpenseDate = date,
            PaymentMethod = method,
            CashSessionId = cashSessionId
        });
        Db.SaveChanges();
    }

    private async Task<Supplier> AddSupplierAsync(string name)
    {
        var supplier = await _db.AddSupplierAsync(name, code: name[..3].ToUpper());
        return supplier;
    }

    private async Task<int> AddPurchaseReceivedAsync(int supplierId, decimal total, decimal paid, DateTime? dueDate)
    {
        var purchase = new Purchase
        {
            PurchaseNumber = $"PUR-{Guid.NewGuid():N}"[..12],
            SupplierId = supplierId,
            PurchaseDate = Jan,
            DueDate = dueDate,
            Subtotal = total,
            TotalAmount = total,
            AmountPaid = paid,
            Status = PurchaseStatus.Received,
            PaymentStatus = paid >= total ? PaymentStatus.Paid : paid > 0 ? PaymentStatus.PartiallyPaid : PaymentStatus.Unpaid
        };
        Db.Purchases.Add(purchase);
        Db.SaveChanges();
        return purchase.Id;
    }

    private void AddSupplierPayment(int purchaseId, decimal amount, DateTime paidAt, PaymentMethod method)
    {
        Db.SupplierPayments.Add(new SupplierPayment { PurchaseId = purchaseId, Amount = amount, PaidAt = paidAt, Method = method });
        Db.SaveChanges();
    }

    [Fact]
    public async Task Summary_ComputesProfitAndCash()
    {
        var sale = AddSaleFull(1000m, 100m, 400m, new DateTime(2026, 1, 10), payments: (PaymentMethod.Cash, 900m));
        AddSaleFull(500m, 0m, 200m, new DateTime(2026, 1, 11), SaleStatus.Voided, payments: (PaymentMethod.Cash, 500m));
        AddSaleFull(300m, 0m, 100m, new DateTime(2025, 12, 31), payments: (PaymentMethod.Cash, 300m));
        AddReturn(sale.Id, 50m, new DateTime(2026, 1, 15));
        AddExpense(200m, new DateTime(2026, 1, 12), PaymentMethod.Cash);

        var summary = await Service.GetSummaryAsync(Jan, Feb);

        Assert.Equal(1000m, summary.GrossSales);
        Assert.Equal(100m, summary.SalesDiscounts);
        Assert.Equal(50m, summary.SalesReturns);
        Assert.Equal(850m, summary.NetSales);
        Assert.Equal(400m, summary.CostOfGoodsSold);
        Assert.Equal(450m, summary.GrossProfit);
        Assert.Equal(200m, summary.OperatingExpenses);
        Assert.Equal(250m, summary.OperatingProfit);
        Assert.Equal(900m, summary.CashInflows);
        Assert.Equal(200m, summary.CashOutflows);
        Assert.Equal(700m, summary.OperatingCashFlow);
        Assert.Equal(52.94m, summary.GrossMarginPercent);
        Assert.Equal(29.41m, summary.NetMarginPercent);
    }

    [Fact]
    public async Task Summary_NoData_IsAllZero()
    {
        var summary = await Service.GetSummaryAsync(Jan, Feb);
        Assert.Equal(0m, summary.NetSales);
        Assert.Equal(0m, summary.OperatingProfit);
        Assert.Equal(0m, summary.GrossMarginPercent);
    }

    [Fact]
    public async Task CashFlow_SplitsCashOnly()
    {
        var supplier = await AddSupplierAsync("Acme");
        var purchaseId = await AddPurchaseReceivedAsync(supplier.Id, 1000m, 0m, null);
        AddSaleFull(900m, 0m, 300m, new DateTime(2026, 1, 10), payments: (PaymentMethod.Cash, 900m));
        AddSupplierPayment(purchaseId, 300m, new DateTime(2026, 1, 12), PaymentMethod.Cash);
        AddSupplierPayment(purchaseId, 100m, new DateTime(2026, 1, 12), PaymentMethod.BankTransfer);
        AddExpense(200m, new DateTime(2026, 1, 12), PaymentMethod.Cash);
        AddExpense(150m, new DateTime(2026, 1, 12), PaymentMethod.Card);

        var flow = await Service.GetCashFlowAsync(Jan, Feb);

        Assert.Equal(900m, flow.CashInflows);
        Assert.Equal(500m, flow.CashOutflows);
        Assert.Equal(400m, flow.NetCashFlow);
        Assert.Equal(900m, flow.Inflows.Single(l => l.Label == "Cash sales").Amount);
        Assert.Equal(300m, flow.Outflows.Single(l => l.Label == "Supplier payments").Amount);
        Assert.Equal(200m, flow.Outflows.Single(l => l.Label == "Operating expenses").Amount);
    }

    [Fact]
    public async Task SalesPaymentBreakdown_GroupsByMethod()
    {
        AddSaleFull(1000m, 0m, 0m, new DateTime(2026, 1, 10), payments: new[] { (PaymentMethod.Cash, 600m), (PaymentMethod.Card, 400m) });

        var breakdown = await Service.GetSalesPaymentBreakdownAsync(Jan, Feb);

        Assert.Equal(2, breakdown.Count);
        var cash = breakdown.Single(b => b.Method == PaymentMethod.Cash);
        var card = breakdown.Single(b => b.Method == PaymentMethod.Card);
        Assert.Equal(600m, cash.TotalAmount);
        Assert.Equal(400m, card.TotalAmount);
        Assert.Equal(60m, cash.Percentage);
        Assert.Equal(40m, card.Percentage);
    }

    [Fact]
    public async Task SupplierLiabilities_OutstandingAndAgeing()
    {
        var a = await AddSupplierAsync("Alpha");
        var b = await AddSupplierAsync("Beta");
        await AddPurchaseReceivedAsync(a.Id, 1000m, 400m, DateTime.Today.AddDays(-100));
        await AddPurchaseReceivedAsync(b.Id, 500m, 500m, null);

        var liabilities = await Service.GetSupplierLiabilitiesAsync();

        Assert.Equal(1, liabilities.SupplierCount);
        Assert.Equal(600m, liabilities.TotalOutstanding);
        Assert.Equal(600m, liabilities.OverdueTotal);
        var line = liabilities.Lines.Single();
        Assert.Equal("Alpha", line.SupplierName);
        Assert.Equal(1000m, line.ReceivedValue);
        Assert.Equal(400m, line.AmountPaid);
        Assert.Equal(600m, line.Outstanding);
        Assert.Equal("90+ days", line.AgeingBucket);
    }

    [Fact]
    public async Task SupplierStatement_ReturnsReceivedPaidAndClosing()
    {
        var a = await AddSupplierAsync("Alpha");
        var id = await AddPurchaseReceivedAsync(a.Id, 1000m, 400m, DateTime.Today.AddDays(-10));

        var statement = await Service.GetSupplierStatementAsync(a.Id);

        Assert.Equal(a.Id, statement.SupplierId);
        Assert.Equal(1000m, statement.ReceivedValue);
        Assert.Equal(400m, statement.AmountPaid);
        Assert.Equal(600m, statement.Outstanding);
    }

    [Fact]
    public async Task CashReconciliation_OpenAndClosed()
    {
        var open = _db.AddCashSession(open: true);
        // link a cash sale and cash expense to the open session
        var sale = AddSaleFull(300m, 0m, 0m, DateTime.Now, cashSessionId: open.Id, payments: (PaymentMethod.Cash, 300m));
        AddExpense(50m, DateTime.Now, PaymentMethod.Cash, open.Id);

        var closed = _db.AddCashSession(open: false);
        Db.CashSessions.Single(s => s.Id == closed.Id).OpeningFloat = 50m;
        Db.CashSessions.Single(s => s.Id == closed.Id).ClosingCounted = 400m;
        Db.SaveChanges();

        var recon = await Service.GetCashReconciliationAsync();
        var openRow = recon.Single(r => r.SessionId == open.Id);
        var closedRow = recon.Single(r => r.SessionId == closed.Id);

        Assert.True(openRow.IsOpen);
        Assert.Equal(300m, openRow.CashInflows);
        Assert.Equal(50m, openRow.CashOutflows);
        Assert.Equal(250m, openRow.ExpectedClosing);
        Assert.Null(openRow.ActualClosing);
        Assert.Null(openRow.Variance);

        Assert.False(closedRow.IsOpen);
        Assert.Equal(50m, closedRow.ExpectedClosing);
        Assert.Equal(400m, closedRow.ActualClosing);
        Assert.Equal(350m, closedRow.Variance);
    }

    [Fact]
    public async Task MonthlySeries_LengthAndValues()
    {
        AddSaleFull(1000m, 100m, 400m, DateTime.Now);

        var series = await Service.GetMonthlySeriesAsync(6);

        Assert.Equal(6, series.Count);
        var current = series[^1];
        Assert.Equal(900m, current.NetSales);
        Assert.Equal(500m, current.GrossProfit);
    }

    [Fact]
    public async Task FinancialReporting_IsReadOnly()
    {
        var supplier = await AddSupplierAsync("Acme");
        await AddPurchaseReceivedAsync(supplier.Id, 1000m, 0m, null);
        var sale = AddSaleFull(500m, 0m, 200m, DateTime.Now, payments: (PaymentMethod.Cash, 500m));
        AddExpense(100m, DateTime.Now, PaymentMethod.Cash);
        AddReturn(sale.Id, 20m, DateTime.Now);
        var saleCount = await Db.Sales.CountAsync();
        var purchaseCount = await Db.Purchases.CountAsync();
        var expenseCount = await Db.Expenses.CountAsync();
        var paymentCount = await Db.Payments.CountAsync();

        await Service.GetSummaryAsync(Jan, Feb);
        await Service.GetCashFlowAsync(Jan, Feb);
        await Service.GetSupplierLiabilitiesAsync();
        await Service.GetCashReconciliationAsync();

        Assert.Equal(saleCount, await Db.Sales.CountAsync());
        Assert.Equal(purchaseCount, await Db.Purchases.CountAsync());
        Assert.Equal(expenseCount, await Db.Expenses.CountAsync());
        Assert.Equal(paymentCount, await Db.Payments.CountAsync());
        var purchase = await Db.Purchases.SingleAsync();
        Assert.Equal(1000m, purchase.TotalAmount);
        Assert.Equal(0m, purchase.AmountPaid);
    }
}