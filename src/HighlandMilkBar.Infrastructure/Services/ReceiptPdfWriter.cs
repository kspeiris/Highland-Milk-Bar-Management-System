using System.Text;
using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Writes a receipt to a minimal, dependency-free single-page PDF (Courier, monospaced). This
/// provides the "save receipt as PDF" capability without any third-party PDF library.
/// </summary>
public static class ReceiptPdfWriter
{
    public static void Write(string path, ReceiptDocument doc)
    {
        var text = ReceiptTextRenderer.Render(doc).Replace("\r\n", "\n");
        var lines = text.Split('\n');

        var stream = new StringBuilder();
        stream.Append("BT\n");
        stream.Append("/F1 9 Tf\n");
        stream.Append("14 TL\n");
        stream.Append("40 800 Td\n");
        foreach (var line in lines)
            stream.Append('(').Append(Escape(line)).Append(") Tj T*\n");
        stream.Append("ET\n");

        var streamText = stream.ToString();

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
            "/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(streamText)} >>\nstream\n{streamText}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>"
        };

        using var buffer = new MemoryStream();

        void Write(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            buffer.Write(bytes, 0, bytes.Length);
        }

        var offsets = new List<long>();
        Write("%PDF-1.4\n");
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(buffer.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefStart = buffer.Position;
        Write($"xref\n0 {objects.Count + 1}\n");
        Write("0000000000 65535 f \n");
        foreach (var offset in offsets)
            Write($"{offset:D10} 00000 n \n");

        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF\n");

        File.WriteAllBytes(path, buffer.ToArray());
    }

    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\r': break;
                default:
                    sb.Append(c < 32 || c > 126 ? '?' : c);
                    break;
            }
        }
        return sb.ToString();
    }
}