using System.Text;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Renders a <see cref="ReceiptDocument"/> to fixed-width plain text. Kept free of any UI or
/// printing dependency so it can be unit-tested and reused by print/PDF/thermal output.
/// </summary>
public static class ReceiptTextRenderer
{
    public static string Render(ReceiptDocument doc)
    {
        var width = doc.Width;
        var lines = new List<string>();

        void Center(string text)
        {
            if (string.IsNullOrEmpty(text)) { lines.Add(string.Empty); return; }
            var trimmed = text.Length <= width ? text : text[..width];
            var pad = width - trimmed.Length;
            var left = pad / 2;
            lines.Add(new string(' ', left) + trimmed);
        }

        void Left(string text)
        {
            if (string.IsNullOrEmpty(text)) { lines.Add(string.Empty); return; }
            lines.Add(text.Length <= width ? text : text[..width]);
        }

        void Pair(string left, string right)
        {
            left ??= string.Empty;
            right ??= string.Empty;
            if (left.Length + right.Length + 1 >= width)
            {
                var maxLeft = Math.Max(0, width - right.Length - 1);
                left = left.Length > maxLeft ? left[..maxLeft] : left;
            }
            var spaces = Math.Max(1, width - left.Length - right.Length);
            lines.Add(left + new string(' ', spaces) + right);
        }

        void Rule() => lines.Add(new string('-', width));

        Center(doc.OutletName);
        if (!string.IsNullOrWhiteSpace(doc.OutletAddress))
            foreach (var l in Wrap(doc.OutletAddress, width)) Center(l);
        if (!string.IsNullOrWhiteSpace(doc.OutletPhone)) Center(doc.OutletPhone);

        Rule();
        Pair("Invoice:", doc.InvoiceNumber);
        Pair("Date:", doc.SoldAt.ToString("yyyy-MM-dd HH:mm"));
        if (!string.IsNullOrWhiteSpace(doc.CustomerName)) Pair("Customer:", doc.CustomerName);
        Rule();

        foreach (var item in doc.Items)
        {
            Left(item.Name);
            Pair($"  {item.Quantity:0.###} x {item.UnitPrice:0.00}", item.LineTotal.ToString("0.00"));
        }

        Rule();
        Pair("Subtotal", doc.Subtotal.ToString("0.00"));
        if (doc.Discount > 0) Pair("Discount", "-" + doc.Discount.ToString("0.00"));
        Pair("TOTAL", doc.Total.ToString("0.00"));
        Rule();

        foreach (var payment in doc.Payments)
            Pair(payment.Method, payment.Amount.ToString("0.00"));
        Pair("Paid", doc.AmountPaid.ToString("0.00"));
        if (doc.Change > 0) Pair("Change", doc.Change.ToString("0.00"));

        if (!string.IsNullOrWhiteSpace(doc.Footer))
        {
            Rule();
            foreach (var l in Wrap(doc.Footer, width)) Center(l);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length == 0) current.Append(word);
            else if (current.Length + 1 + word.Length <= width) current.Append(' ').Append(word);
            else
            {
                yield return current.ToString();
                current.Clear();
                current.Append(word);
            }
        }
        if (current.Length > 0) yield return current.ToString();
    }
}