using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Read-only financial reporting built entirely from existing ledgers.
/// SQLite cannot perform server-side decimal aggregates, so amount columns are loaded
/// into memory and summed with .NET <see cref="decimal"/> arithmetic for exact currency.
/// </summary>
public sealed class FinancialQueryService(AppDbContext db) : IFinancialQueryService
{
    public async Task<FinancialSummaryDto> GetSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.SoldAt >= from && s.SoldAt < to && s.Status != SaleStatus.Voided)
            .Select(s => new { s.Subtotal, s.DiscountAmount, s.CostOfGoodsSold })
            .ToListAsync(cancellationToken);

        var grossSales = sales.Sum(x => x.Subtotal);
        var discounts = sales.Sum(x => x.DiscountAmount);
        var cogs = sales.Sum(x => x.CostOfGoodsSold);

        var returnAmounts = await db.SalesReturns.AsNoTracking()
            .Where(r => r.ReturnedAt >= from && r.ReturnedAt < to && r.Status == SalesReturnStatus.Completed)
            .Select(r => r.TotalAmount)
            .ToListAsync(cancellationToken);
        var salesReturns = returnAmounts.Sum();

        var expenseAmounts = await db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate < to)
            .Select(e => e.Amount)
            .ToListAsync(cancellationToken);
        var operatingExpenses = expenseAmounts.Sum();

        var netSales = grossSales - discounts - salesReturns;
        var grossProfit = netSales - cogs;
        var operatingProfit = grossProfit - operatingExpenses;

        var cashInflows = await SumCashSalePaymentsAsync(from, to, cancellationToken);
        var cashSupplierPayments = await SumSupplierPaymentsAsync(from, to, PaymentMethod.Cash, cancellationToken);
        var cashExpenses = await SumCashExpensesAsync(from, to, cancellationToken);
        var cashOutflows = cashSupplierPayments + cashExpenses;

        var supplierOutstanding = await SumSupplierOutstandingAsync(cancellationToken);

        return new FinancialSummaryDto
        {
            From = from,
            To = to,
            GrossSales = Round(grossSales),
            SalesDiscounts = Round(discounts),
            SalesReturns = Round(salesReturns),
            NetSales = Round(netSales),
            CostOfGoodsSold = Round(cogs),
            GrossProfit = Round(grossProfit),
            OperatingExpenses = Round(operatingExpenses),
            OperatingProfit = Round(operatingProfit),
            CashInflows = Round(cashInflows),
            CashOutflows = Round(cashOutflows),
            OperatingCashFlow = Round(cashInflows - cashOutflows),
            SupplierOutstanding = Round(supplierOutstanding),
            GrossMarginPercent = Round(netSales == 0m ? 0m : grossProfit / netSales * 100m),
            NetMarginPercent = Round(netSales == 0m ? 0m : operatingProfit / netSales * 100m)
        };
    }

    public async Task<CashFlowDto> GetCashFlowAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);

        var cashSales = await SumCashSalePaymentsAsync(from, to, cancellationToken);
        var cashSupplier = await SumSupplierPaymentsAsync(from, to, PaymentMethod.Cash, cancellationToken);
        var cashExpenses = await SumCashExpensesAsync(from, to, cancellationToken);

        var inflowCount = await db.Payments.AsNoTracking()
            .CountAsync(p => p.PaidAt >= from && p.PaidAt < to && p.Method == PaymentMethod.Cash && p.Sale!.Status != SaleStatus.Voided, cancellationToken);
        var supplierPayCount = await db.SupplierPayments.AsNoTracking()
            .CountAsync(p => p.PaidAt >= from && p.PaidAt < to && p.Method == PaymentMethod.Cash, cancellationToken);
        var expensePayCount = await db.Expenses.AsNoTracking()
            .CountAsync(e => e.ExpenseDate >= from && e.ExpenseDate < to && e.PaymentMethod == PaymentMethod.Cash, cancellationToken);

        var inflows = new List<CashFlowLineDto>
        {
            new() { Label = "Cash sales", Amount = Round(cashSales), Count = inflowCount },
            new() { Label = "Receivable settlements", Amount = 0m, Count = 0 },
            new() { Label = "Other cash in", Amount = 0m, Count = 0 }
        };
        var outflows = new List<CashFlowLineDto>
        {
            new() { Label = "Supplier payments", Amount = Round(cashSupplier), Count = supplierPayCount },
            new() { Label = "Operating expenses", Amount = Round(cashExpenses), Count = expensePayCount },
            new() { Label = "Other cash out", Amount = 0m, Count = 0 }
        };

        var totalIn = cashSales;
        var totalOut = cashSupplier + cashExpenses;

        return new CashFlowDto
        {
            From = from,
            To = to,
            CashInflows = Round(totalIn),
            CashOutflows = Round(totalOut),
            NetCashFlow = Round(totalIn - totalOut),
            Inflows = inflows,
            Outflows = outflows
        };
    }

    public async Task<IReadOnlyList<PaymentBreakdownDto>> GetSalesPaymentBreakdownAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);

        var rows = await db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= from && p.PaidAt < to && p.Sale!.Status != SaleStatus.Voided)
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync(cancellationToken);

        return BuildBreakdown(rows.Select(r => (r.Method, r.Amount)));
    }

    public async Task<IReadOnlyList<PaymentBreakdownDto>> GetSupplierPaymentBreakdownAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);

        var rows = await db.SupplierPayments.AsNoTracking()
            .Where(p => p.PaidAt >= from && p.PaidAt < to)
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync(cancellationToken);

        return BuildBreakdown(rows.Select(r => (r.Method, r.Amount)));
    }

    public async Task<IReadOnlyList<PaymentBreakdownDto>> GetExpensePaymentBreakdownAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);

        var rows = await db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate < to)
            .Select(e => new { e.PaymentMethod, e.Amount })
            .ToListAsync(cancellationToken);

        return BuildBreakdown(rows.Select(r => (r.PaymentMethod, r.Amount)));
    }

    public async Task<SupplierLiabilityDto> GetSupplierLiabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var asOf = DateTime.Today;

        var rows = await db.Purchases.AsNoTracking()
            .Where(p => p.Status == PurchaseStatus.Received && p.SupplierId != null)
            .Select(p => new
            {
                p.SupplierId,
                SupplierName = p.Supplier != null ? p.Supplier.Name : string.Empty,
                SupplierCode = p.Supplier != null ? p.Supplier.Code : null,
                p.TotalAmount,
                p.AmountPaid,
                p.DueDate
            })
            .ToListAsync(cancellationToken);

        var groups = rows
            .GroupBy(r => r.SupplierId!.Value)
            .Select(g =>
            {
                var paid = g.Sum(r => r.AmountPaid);
                var outstanding = g.Sum(r => r.TotalAmount - r.AmountPaid);
                var unpaid = g.Where(r => r.TotalAmount - r.AmountPaid > 0m).ToList();
                var earliestDue = unpaid.Where(r => r.DueDate != null).Select(r => r.DueDate!.Value).DefaultIfEmpty().Min();
                var overdue = unpaid
                    .Where(r => r.DueDate != null && r.DueDate.Value.Date < asOf)
                    .Sum(r => r.TotalAmount - r.AmountPaid);

                var line = new SupplierLiabilityLineDto
                {
                    SupplierId = g.Key,
                    SupplierCode = g.First().SupplierCode ?? string.Empty,
                    SupplierName = g.First().SupplierName,
                    ReceivedValue = Round(g.Sum(r => r.TotalAmount)),
                    AmountPaid = Round(paid),
                    Outstanding = Round(outstanding),
                    EarliestDueDate = unpaid.Any(r => r.DueDate != null) ? earliestDue : null,
                    OverdueAmount = Round(overdue),
                    AgeingBucket = AgeingBucket(earliestDue, asOf)
                };
                return line;
            })
            .OrderByDescending(l => l.Outstanding)
            .ToList();

        var lines = groups
            .Where(l => l.Outstanding > 0m)
            .OrderByDescending(l => l.Outstanding)
            .ToList();
        var buckets = BuildAgeingBuckets(lines);

        return new SupplierLiabilityDto
        {
            AsOf = asOf,
            TotalReceived = Round(rows.Sum(r => r.TotalAmount)),
            TotalPaid = Round(rows.Sum(r => r.AmountPaid)),
            TotalOutstanding = Round(lines.Sum(l => l.Outstanding)),
            OverdueTotal = Round(lines.Sum(l => l.OverdueAmount)),
            SupplierCount = lines.Count(l => l.Outstanding > 0m),
            Lines = lines,
            AgeingBuckets = buckets
        };
    }

    public async Task<SupplierLiabilityLineDto> GetSupplierStatementAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.AsNoTracking()
            .Where(s => s.Id == supplierId)
            .Select(s => new { s.Id, s.Name, s.Code })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Supplier {supplierId} was not found.");

        var purchases = await db.Purchases.AsNoTracking()
            .Where(p => p.Status == PurchaseStatus.Received && p.SupplierId == supplierId)
            .Select(p => new { p.TotalAmount, p.AmountPaid, p.AdditionalCharges, p.DueDate })
            .ToListAsync(cancellationToken);

        var today = DateTime.Today;
        var received = purchases.Sum(p => p.TotalAmount);
        var paid = purchases.Sum(p => p.AmountPaid);
        var outstanding = purchases.Sum(p => p.TotalAmount - p.AmountPaid);
        var unpaid = purchases.Where(p => p.TotalAmount - p.AmountPaid > 0m).ToList();
        var earliest = unpaid.Where(p => p.DueDate != null).Select(p => p.DueDate!.Value).DefaultIfEmpty().Min();

        return new SupplierLiabilityLineDto
        {
            SupplierId = supplier.Id,
            SupplierCode = supplier.Code ?? string.Empty,
            SupplierName = supplier.Name,
            ReceivedValue = Round(received),
            AmountPaid = Round(paid),
            Outstanding = Round(outstanding),
            EarliestDueDate = unpaid.Any(p => p.DueDate != null) ? earliest : null,
            OverdueAmount = Round(unpaid.Where(p => p.DueDate != null && p.DueDate.Value.Date < today).Sum(p => p.TotalAmount - p.AmountPaid)),
            AgeingBucket = AgeingBucket(earliest, today)
        };
    }

    public async Task<IReadOnlyList<FinancialSeriesPointDto>> GetMonthlySeriesAsync(int months = 12, CancellationToken cancellationToken = default)
    {
        if (months < 1) months = 12;
        if (months > 36) months = 36;

        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));
        var end = start.AddMonths(months);

        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.SoldAt >= start && s.SoldAt < end && s.Status != SaleStatus.Voided)
            .Select(s => new { s.SoldAt, s.Subtotal, s.DiscountAmount, s.CostOfGoodsSold })
            .ToListAsync(cancellationToken);
        var returns = await db.SalesReturns.AsNoTracking()
            .Where(r => r.ReturnedAt >= start && r.ReturnedAt < end && r.Status == SalesReturnStatus.Completed)
            .Select(r => new { r.ReturnedAt, r.TotalAmount })
            .ToListAsync(cancellationToken);
        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= start && e.ExpenseDate < end)
            .Select(e => new { e.ExpenseDate, e.Amount })
            .ToListAsync(cancellationToken);

        var result = new List<FinancialSeriesPointDto>(months);
        for (var i = 0; i < months; i++)
        {
            var month = start.AddMonths(i);
            var next = month.AddMonths(1);

            var mSales = sales.Where(s => s.SoldAt >= month && s.SoldAt < next).ToList();
            var gross = mSales.Sum(s => s.Subtotal);
            var discounts = mSales.Sum(s => s.DiscountAmount);
            var cogs = mSales.Sum(s => s.CostOfGoodsSold);
            var mReturns = returns.Where(r => r.ReturnedAt >= month && r.ReturnedAt < next).Sum(r => r.TotalAmount);
            var mExpenses = expenses.Where(e => e.ExpenseDate >= month && e.ExpenseDate < next).Sum(e => e.Amount);

            var netSales = gross - discounts - mReturns;
            var grossProfit = netSales - cogs;

            result.Add(new FinancialSeriesPointDto
            {
                PeriodLabel = month.ToString("yyyy-MM"),
                GrossSales = Round(gross),
                NetSales = Round(netSales),
                GrossProfit = Round(grossProfit),
                OperatingExpenses = Round(mExpenses),
                OperatingProfit = Round(grossProfit - mExpenses),
                OperatingCashFlow = 0m
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<CashReconciliationDto>> GetCashReconciliationAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await db.CashSessions.AsNoTracking()
            .OrderByDescending(s => s.OpenedAt)
            .Take(50)
            .Select(s => new CashSession
            {
                Id = s.Id,
                OpenedAt = s.OpenedAt,
                ClosedAt = s.ClosedAt,
                OpeningFloat = s.OpeningFloat,
                ClosingCounted = s.ClosingCounted,
                Status = s.Status
            })
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0) return new List<CashReconciliationDto>();

        var sessionIds = sessions.Select(s => s.Id).ToList();

        var cashInflows = await db.Payments.AsNoTracking()
            .Where(p => p.Method == PaymentMethod.Cash && p.Sale != null && p.Sale.CashSessionId != null && p.Sale!.Status != SaleStatus.Voided)
            .Select(p => new { SessionId = p.Sale!.CashSessionId, p.Amount })
            .ToListAsync(cancellationToken);

        var expenseOutflows = await db.Expenses.AsNoTracking()
            .Where(e => e.CashSessionId != null && e.PaymentMethod == PaymentMethod.Cash)
            .Select(e => new { SessionId = e.CashSessionId, e.Amount })
            .ToListAsync(cancellationToken);

        var manual = await db.CashMovements.AsNoTracking()
            .Where(m => m.MovementType == CashMovementType.PayIn || m.MovementType == CashMovementType.Payout)
            .Select(m => new { m.CashSessionId, m.Direction, m.Amount })
            .ToListAsync(cancellationToken);

        var result = new List<CashReconciliationDto>(sessions.Count);
        foreach (var s in sessions)
        {
            var inAmount = cashInflows.Where(r => r.SessionId == s.Id).Sum(r => r.Amount);
            var outAmount = expenseOutflows.Where(r => r.SessionId == s.Id).Sum(r => r.Amount);
            foreach (var m in manual.Where(m => m.CashSessionId == s.Id))
            {
                if (m.Direction == CashDirection.In) inAmount += m.Amount;
                else outAmount += m.Amount;
            }

            var expected = s.OpeningFloat + inAmount - outAmount;

            result.Add(new CashReconciliationDto
            {
                SessionId = s.Id,
                OpenedAt = s.OpenedAt,
                ClosedAt = s.ClosedAt,
                IsOpen = s.Status == CashSessionStatus.Open,
                StatusText = s.Status == CashSessionStatus.Open ? "Open" : "Closed",
                OpeningFloat = Round(s.OpeningFloat),
                CashInflows = Round(inAmount),
                CashOutflows = Round(outAmount),
                ExpectedClosing = Round(expected),
                ActualClosing = s.ClosingCounted.HasValue ? Round(s.ClosingCounted.Value) : null,
                Variance = s.ClosingCounted.HasValue ? Round(s.ClosingCounted.Value - expected) : null
            });
        }

        return result;
    }

    // ---------------- Helpers ----------------

    private async Task<decimal> SumCashSalePaymentsAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var amounts = await db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= from && p.PaidAt < to && p.Method == PaymentMethod.Cash && p.Sale!.Status != SaleStatus.Voided)
            .Select(p => p.Amount)
            .ToListAsync(cancellationToken);
        return amounts.Sum();
    }

    private async Task<decimal> SumSupplierPaymentsAsync(DateTime from, DateTime to, PaymentMethod method, CancellationToken cancellationToken)
    {
        var amounts = await db.SupplierPayments.AsNoTracking()
            .Where(p => p.PaidAt >= from && p.PaidAt < to && p.Method == method)
            .Select(p => p.Amount)
            .ToListAsync(cancellationToken);
        return amounts.Sum();
    }

    private async Task<decimal> SumCashExpensesAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var amounts = await db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate < to && e.PaymentMethod == PaymentMethod.Cash)
            .Select(e => e.Amount)
            .ToListAsync(cancellationToken);
        return amounts.Sum();
    }

    private async Task<decimal> SumSupplierOutstandingAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Purchases.AsNoTracking()
            .Where(p => p.Status == PurchaseStatus.Received)
            .Select(p => new { p.TotalAmount, p.AmountPaid })
            .ToListAsync(cancellationToken);
        return rows.Sum(r => r.TotalAmount - r.AmountPaid);
    }

    private static IReadOnlyList<PaymentBreakdownDto> BuildBreakdown(IEnumerable<(PaymentMethod Method, decimal Amount)> rows)
    {
        var grouped = rows
            .GroupBy(r => r.Method)
            .Select(g => new { Method = g.Key, Total = g.Sum(r => r.Amount), Count = g.Count() })
            .OrderByDescending(g => g.Total)
            .ToList();

        var grand = grouped.Sum(g => g.Total);
        return grouped.Select(g => new PaymentBreakdownDto
        {
            Method = g.Method,
            MethodText = FormatPaymentMethod(g.Method),
            TotalAmount = Round(g.Total),
            TransactionCount = g.Count,
            Percentage = grand == 0m ? 0m : Round(g.Total / grand * 100m)
        }).ToList();
    }

    private static string AgeingBucket(DateTime earliestDue, DateTime asOf)
    {
        var days = (asOf - earliestDue.Date).Days;
        if (days <= 0) return "Current";
        return days switch
        {
            <= 30 => "1-30 days",
            <= 60 => "31-60 days",
            <= 90 => "61-90 days",
            _ => "90+ days"
        };
    }

    private static IReadOnlyList<SupplierAgeingBucketDto> BuildAgeingBuckets(IReadOnlyList<SupplierLiabilityLineDto> lines)
    {
        string[] order = { "Current", "1-30 days", "31-60 days", "61-90 days", "90+ days" };
        return order
            .Select(label =>
            {
                var matching = lines.Where(l => l.AgeingBucket == label && l.Outstanding > 0m).ToList();
                return new SupplierAgeingBucketDto
                {
                    Label = label,
                    Amount = Round(matching.Sum(l => l.Outstanding)),
                    SupplierCount = matching.Count
                };
            })
            .ToList();
    }

    private static string FormatPaymentMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        PaymentMethod.BankTransfer => "Bank transfer",
        PaymentMethod.Cheque => "Cheque",
        PaymentMethod.Other => "Other",
        _ => method.ToString()
    };

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}