using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>Builds receipt documents from completed sales and outlet settings (idempotent reads).</summary>
public sealed class ReceiptService(AppDbContext db) : IReceiptService
{
    public async Task<ReceiptDocument> CreateForSaleAsync(int saleId, ReceiptFormat format, CancellationToken cancellationToken = default)
    {
        var sale = await db.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken)
            ?? throw new InvalidOperationException($"Sale {saleId} was not found.");

        return await BuildAsync(sale, format, cancellationToken);
    }

    public async Task<ReceiptDocument> CreateForReceiptNumberAsync(string receiptNumber, ReceiptFormat format, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
            throw new ArgumentException("A receipt number is required.", nameof(receiptNumber));

        var trimmed = receiptNumber.Trim();
        var sale = await db.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.ReceiptNumber == trimmed, cancellationToken)
            ?? throw new InvalidOperationException($"Receipt '{trimmed}' was not found.");

        return await BuildAsync(sale, format, cancellationToken);
    }

    private async Task<ReceiptDocument> BuildAsync(Sale sale, ReceiptFormat format, CancellationToken cancellationToken)
    {
        var settings = await db.AppSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string Get(string key, string fallback = "") => settings.TryGetValue(key, out var value) ? value : fallback;

        return new ReceiptDocument
        {
            Format = format,
            OutletName = Get("outlet.name", "Highland Milk Bar"),
            OutletAddress = Get("outlet.address"),
            OutletPhone = Get("outlet.phone"),
            Currency = Get("currency.code", "LKR"),
            InvoiceNumber = sale.ReceiptNumber,
            SoldAt = sale.SoldAt,
            CustomerName = sale.Customer?.Name,
            Items = sale.Items
                .OrderBy(i => i.Id)
                .Select(i => new ReceiptItem
                {
                    Name = i.ItemNameSnapshot,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal,
                    IsRecipeItem = i.RecipeId.HasValue
                }).ToList(),
            Subtotal = sale.Subtotal,
            Discount = sale.DiscountAmount,
            Total = sale.TotalAmount,
            Payments = sale.Payments
                .OrderBy(p => p.Id)
                .Select(p => new ReceiptPayment { Method = FormatMethod(p.Method), Amount = p.Amount }).ToList(),
            AmountPaid = sale.AmountPaid,
            Change = sale.AmountPaid - sale.TotalAmount,
            Footer = Get("receipt.footer", "Thank you for your purchase!")
        };
    }

    private static string FormatMethod(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        PaymentMethod.BankTransfer => "Bank Transfer",
        _ => "Other"
    };
}