namespace HighlandMilkBar.Application.DTOs;

/// <summary>Supported receipt layouts.</summary>
public enum ReceiptFormat
{
    /// <summary>58mm thermal roll (~32 characters per line).</summary>
    Thermal58,
    /// <summary>80mm thermal roll (~48 characters per line).</summary>
    Thermal80,
    /// <summary>Full-page A4 invoice.</summary>
    A4
}

/// <summary>A single printed line item on a receipt.</summary>
public sealed class ReceiptItem
{
    public string Name { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public bool IsRecipeItem { get; init; }
}

/// <summary>A payment line printed on a receipt.</summary>
public sealed class ReceiptPayment
{
    public string Method { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

/// <summary>
/// Everything needed to render or reprint a receipt. Deliberately independent of any UI or
/// printing stack so it can be generated, tested and persisted separately from the sale itself.
/// </summary>
public sealed class ReceiptDocument
{
    public ReceiptFormat Format { get; init; } = ReceiptFormat.Thermal80;
    public string OutletName { get; init; } = string.Empty;
    public string? OutletAddress { get; init; }
    public string? OutletPhone { get; init; }
    public string Currency { get; init; } = "LKR";
    public string InvoiceNumber { get; init; } = string.Empty;
    public DateTime SoldAt { get; init; }
    public string? CustomerName { get; init; }

    public IReadOnlyList<ReceiptItem> Items { get; init; } = Array.Empty<ReceiptItem>();
    public decimal Subtotal { get; init; }
    public decimal Discount { get; init; }
    public decimal Total { get; init; }

    public IReadOnlyList<ReceiptPayment> Payments { get; init; } = Array.Empty<ReceiptPayment>();
    public decimal AmountPaid { get; init; }
    public decimal Change { get; init; }

    public string Footer { get; init; } = string.Empty;

    /// <summary>Printable width in characters for the chosen format.</summary>
    public int Width => Format switch
    {
        ReceiptFormat.Thermal58 => 32,
        ReceiptFormat.Thermal80 => 48,
        _ => 80
    };
}