using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.Win32;

namespace HighlandMilkBar.WPF.Views;

public partial class ReceiptWindow : Window
{
    private readonly ReceiptDocument _doc;

    public ReceiptWindow(ReceiptDocument doc)
    {
        InitializeComponent();
        _doc = doc;
        ReceiptText.Text = ReceiptTextRenderer.Render(doc);
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var flowDocument = BuildFlowDocument();
            var dialog = new System.Windows.Controls.PrintDialog();
            if (dialog.ShowDialog() == true)
            {
                var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)flowDocument).DocumentPaginator;
                dialog.PrintDocument(paginator, $"Receipt {_doc.InvoiceNumber}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Printing failed: {ex.Message}", "Print error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SavePdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF file (*.pdf)|*.pdf",
            FileName = $"{_doc.InvoiceNumber}.pdf"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ReceiptPdfWriter.Write(dialog.FileName, _doc);
            MessageBox.Show(this, "Receipt saved as PDF.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private FlowDocument BuildFlowDocument()
    {
        var pageWidth = _doc.Format == ReceiptFormat.A4 ? 780d : 300d;
        var font = new FontFamily("Courier New");

        var document = new FlowDocument
        {
            PageWidth = pageWidth,
            FontFamily = font,
            FontSize = 11,
            PagePadding = new Thickness(18)
        };

        var lines = ReceiptTextRenderer.Render(_doc).Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            var paragraph = new Paragraph(new Run(line))
            {
                FontFamily = font,
                FontSize = 11,
                Margin = new Thickness(0)
            };
            document.Blocks.Add(paragraph);
        }

        return document;
    }
}