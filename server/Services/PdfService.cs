using LBElectronica.Server.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LBElectronica.Server.Services;

public class PdfService
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "yyyy-MM-dd HH:mm"];

    public byte[] SalesSummary(string title, IEnumerable<(string Label, decimal Amount)> lines, DateTime start, DateTime end, string generatedBy = "Sistema")
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Element(x => BuildHeader(x, title, generatedBy, start, end));
                page.Content().Column(col =>
                {
                    foreach (var line in lines)
                    {
                        col.Item().PaddingVertical(2).Row(r =>
                        {
                            r.RelativeItem().Text(line.Label).SemiBold();
                            r.ConstantItem(140).AlignRight().Text(FormatMoney(line.Amount)).Bold();
                        });
                    }
                });
                page.Footer().AlignRight().Text(t => t.Span("LB Electronica").FontSize(9).FontColor(Colors.Grey.Darken1));
            });
        }).GeneratePdf();
    }

    public byte[] TableReport(string title, List<string> headers, List<List<string>> rows, DateTime start, DateTime end, string generatedBy = "Sistema")
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Element(x => BuildHeader(x, title, generatedBy, start, end));
                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        for (var i = 0; i < headers.Count; i++) c.RelativeColumn();
                    });

                    foreach (var h in headers)
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(h).SemiBold();

                    foreach (var row in rows)
                    {
                        foreach (var cell in row)
                            table.Cell().Padding(4).Text(cell ?? string.Empty).FontSize(10);
                    }
                });
                page.Footer().AlignRight().Text(t => t.Span("LB Electronica").FontSize(9).FontColor(Colors.Grey.Darken1));
            });
        }).GeneratePdf();
    }

    public byte[] CashReport(DateTime start, DateTime end, IEnumerable<(DateTime Date, string Reference, decimal Income, decimal Expense)> rows,
        decimal totalIncome, decimal totalExpense, decimal balance, string generatedBy = "Sistema")
    {
        var tableRows = rows.Select(x => new List<string>
        {
            x.Date.ToString("dd/MM/yyyy HH:mm"),
            x.Reference,
            x.Income > 0 ? FormatMoney(x.Income) : "-",
            x.Expense > 0 ? FormatMoney(x.Expense) : "-"
        }).ToList();

        tableRows.Add(new() { "", "TOTAL", FormatMoney(totalIncome), FormatMoney(totalExpense) });
        tableRows.Add(new() { "", "SALDO EN CAJA", FormatMoney(balance), "" });

        return TableReport("Reporte de Caja", new() { "Fecha", "Referencia", "Ingresos", "Egresos" }, tableRows, start, end, generatedBy);
    }

    public byte[] InventoryReport(IEnumerable<Product> products, string generatedBy = "Sistema")
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var now = DateTime.Now;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.Header().Element(x => BuildHeader(x, "Reporte de Inventario", generatedBy, now.Date, now));
                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(4);
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    table.Cell().Text("Código").SemiBold();
                    table.Cell().Text("Producto").SemiBold();
                    table.Cell().Text("Stock").SemiBold();
                    table.Cell().Text("Valor costo").SemiBold();

                    foreach (var p in products)
                    {
                        table.Cell().Text(p.InternalCode);
                        table.Cell().Text(p.Name);
                        table.Cell().Text(p.StockQuantity.ToString("0.##"));
                        table.Cell().Text(FormatMoney(p.StockQuantity * p.CostPrice));
                    }
                });
            });
        }).GeneratePdf();
    }

    private static string FormatMoney(decimal value) => value.ToString("N2");

    private static byte[]? TryLoadLogo()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "logo_lb.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logo_lb.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "client", "public", "logo_lb.png")
        };

        var file = candidates.FirstOrDefault(File.Exists);
        if (file is null) return null;
        try { return File.ReadAllBytes(file); }
        catch { return null; }
    }

    private static void BuildHeader(IContainer container, string title, string generatedBy, DateTime start, DateTime end)
    {
        var logo = TryLoadLogo();

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("LB Electronica").Bold().FontSize(18);
                    c.Item().Text(title).SemiBold().FontSize(13).FontColor(Colors.Blue.Medium);
                    c.Item().Text($"Período: {start:dd/MM/yyyy} - {end:dd/MM/yyyy}").FontSize(10);
                    c.Item().Text($"Generado por: {generatedBy}").FontSize(10);
                    c.Item().Text($"Fecha y hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(10);
                });

                if (logo is not null)
                {
                    row.ConstantItem(120).Height(40).AlignRight().Image(logo, ImageScaling.FitArea);
                }
            });

            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }
}
