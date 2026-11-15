using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Loyalty configuration (AppSettings key/value) and the append-only points ledger.
/// Awarding and reversal run inside the caller's sale/return transaction (same
/// <see cref="AppDbContext"/>), so the ledger commits atomically with the sale.
/// </summary>
public sealed class LoyaltyService(AppDbContext db) : ILoyaltyService
{
    private const string KeyEnabled = "loyalty.enabled";
    private const string KeyPointsPerHundred = "loyalty.points_per_hundred";
    private const string KeyMinimum = "loyalty.minimum_qualifying_amount";

    public async Task<LoyaltySettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == KeyEnabled || s.Key == KeyPointsPerHundred || s.Key == KeyMinimum)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var enabled = settings.TryGetValue(KeyEnabled, out var e) && bool.TryParse(e, out var b) && b;
        var pointsPerHundred = ParseDecimal(settings, KeyPointsPerHundred, 1m);
        var minimum = ParseDecimal(settings, KeyMinimum, 500m);

        return new LoyaltySettingsDto
        {
            Enabled = settings.ContainsKey(KeyEnabled) ? enabled : true,
            PointsPerHundred = pointsPerHundred,
            MinimumQualifyingAmount = minimum
        };
    }

    public async Task UpdateSettingsAsync(LoyaltyUpdateSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PointsPerHundred < 0)
            throw new ArgumentException("Points per hundred cannot be negative.", nameof(request));
        if (request.MinimumQualifyingAmount < 0)
            throw new ArgumentException("Minimum qualifying amount cannot be negative.", nameof(request));

        await UpsertSettingAsync(KeyEnabled, request.Enabled ? "true" : "false", cancellationToken);
        await UpsertSettingAsync(KeyPointsPerHundred,
            request.PointsPerHundred.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
        await UpsertSettingAsync(KeyMinimum,
            request.MinimumQualifyingAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
    }

    public async Task<LoyaltyBalanceDto> GetBalanceAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var rows = await db.LoyaltyTransactions.AsNoTracking()
            .Where(l => l.CustomerId == customerId)
            .Select(l => new { l.Type, l.Points })
            .ToListAsync(cancellationToken);

        var balance = rows.Sum(r => r.Points);
        var earned = rows.Where(r => r.Points > 0).Sum(r => r.Points);
        var redeemed = -rows.Where(r => r.Type == LoyaltyTransactionType.Redeemed).Sum(r => r.Points);
        var reversed = -rows.Where(r => r.Type == LoyaltyTransactionType.Reversed).Sum(r => r.Points);

        return new LoyaltyBalanceDto
        {
            CustomerId = customerId,
            Balance = balance,
            TotalEarned = earned,
            TotalRedeemed = redeemed,
            TotalReversed = reversed
        };
    }

    public async Task<IReadOnlyList<LoyaltyTransactionDto>> GetHistoryAsync(int customerId, int take = 100, CancellationToken cancellationToken = default)
    {
        if (take < 1) take = 100;
        if (take > 500) take = 500;

        return await db.LoyaltyTransactions.AsNoTracking()
            .Where(l => l.CustomerId == customerId)
            .OrderByDescending(l => l.TransactionDate)
            .ThenByDescending(l => l.Id)
            .Take(take)
            .Select(l => new LoyaltyTransactionDto
            {
                Id = l.Id,
                TransactionDate = l.TransactionDate,
                Type = l.Type,
                TypeText = FormatType(l.Type),
                Points = l.Points,
                BalanceAfter = l.BalanceAfter,
                Description = l.Description,
                Reference = l.Reference
            })
            .ToListAsync(cancellationToken);
    }

    public async Task AwardForSaleAsync(int saleId, int customerId, decimal saleTotal, string receiptNumber, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0) return; // walk-in sale

        var settings = await GetSettingsAsync(cancellationToken);
        var points = LoyaltyCalculator.PointsForSale(
            saleTotal, settings.PointsPerHundred, settings.MinimumQualifyingAmount, settings.Enabled);
        if (points <= 0) return;

        // Prevent awarding twice for the same sale.
        var already = await db.LoyaltyTransactions.AnyAsync(
            l => l.SaleId == saleId && l.Type == LoyaltyTransactionType.Earned, cancellationToken);
        if (already) return;

        var balance = await db.LoyaltyTransactions
            .Where(l => l.CustomerId == customerId)
            .SumAsync(l => l.Points, cancellationToken);

        db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            CustomerId = customerId,
            SaleId = saleId,
            Type = LoyaltyTransactionType.Earned,
            Points = points,
            BalanceAfter = balance + points,
            TransactionDate = DateTime.Now,
            Description = "Loyalty points earned",
            Reference = receiptNumber,
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReverseForReturnAsync(
        int saleId,
        int customerId,
        decimal saleTotal,
        decimal returnedAmount,
        bool isFullReturn,
        int salesReturnId,
        string returnNumber,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0) return;

        var earnedTxn = await db.LoyaltyTransactions.FirstOrDefaultAsync(
            l => l.SaleId == saleId && l.Type == LoyaltyTransactionType.Earned, cancellationToken);
        if (earnedTxn is null) return;

        var alreadyReversed = await db.LoyaltyTransactions
            .Where(l => l.SaleId == saleId && l.Type == LoyaltyTransactionType.Reversed)
            .SumAsync(l => l.Points, cancellationToken); // negative values
        var reversedSoFar = -alreadyReversed;
        var remaining = earnedTxn.Points - reversedSoFar;
        if (remaining <= 0) return;

        var toReverse = isFullReturn
            ? remaining
            : Math.Min(remaining, LoyaltyCalculator.PointsToReverse(earnedTxn.Points, saleTotal, returnedAmount));
        if (toReverse <= 0) return;

        var balance = await db.LoyaltyTransactions
            .Where(l => l.CustomerId == customerId)
            .SumAsync(l => l.Points, cancellationToken);

        db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            CustomerId = customerId,
            SaleId = saleId,
            SalesReturnId = salesReturnId,
            Type = LoyaltyTransactionType.Reversed,
            Points = -toReverse,
            BalanceAfter = balance - toReverse,
            TransactionDate = DateTime.Now,
            Description = isFullReturn ? "Loyalty points reversed (full return)" : "Loyalty points reversed (partial return)",
            Reference = returnNumber,
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var existing = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (existing is null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.Now });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static decimal ParseDecimal(System.Collections.Generic.Dictionary<string, string> settings, string key, decimal fallback)
    {
        if (settings.TryGetValue(key, out var raw) &&
            decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return fallback;
    }

    private static string FormatType(LoyaltyTransactionType type) => type switch
    {
        LoyaltyTransactionType.Earned => "Earned",
        LoyaltyTransactionType.Reversed => "Reversed",
        LoyaltyTransactionType.Redeemed => "Redeemed",
        LoyaltyTransactionType.Adjustment => "Adjustment",
        _ => type.ToString()
    };
}