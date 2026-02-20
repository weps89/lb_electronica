using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LBElectronica.Server.Services;

public enum PdfCellAlignment
{
    Left,
    Center,
    Right
}

public record PdfReportMetadata(
    string GeneratedBy,
    string Role,
    DateTime GeneratedAt,
    string Filters,
    string BranchOrStore
);

public record PdfSummaryCard(string Title, string Value);

public record PdfColumnDefinition(string Key, string Label, PdfCellAlignment Alignment = PdfCellAlignment.Left);
public record PdfTotalsBar(string Label, string Value);

public record PdfReportRequest(
    string SystemName,
    string ModuleName,
    string Title,
    PdfReportMetadata Metadata,
    DateTime? Start,
    DateTime? End,
    IReadOnlyList<PdfSummaryCard> SummaryCards,
    IReadOnlyList<PdfColumnDefinition> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<PdfTotalsBar>? TotalsBar = null,
    bool? ForceLandscape = null
);

public class PdfReportService
{
    private const string Accent = "#2F7DFF";
    private const string Text = "#333333";
    private const string Muted = "#666666";
    private const string Line = "#D9D9D9";
    private const string HeaderBg = "#F2F2F2";
    private const string Zebra = "#F9FAFB";
    private const string CardBg = "#F5F8FF";

    public byte[] Generate(PdfReportRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var isLandscape = request.ForceLandscape ?? request.Columns.Count > 6;
        var logo = TryLoadLogo();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(isLandscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontFamily("DejaVu Sans").FontSize(9).FontColor(Text));

                page.Header().Element(x => BuildHeader(x, request, logo));
                page.Content().Element(x => BuildContent(x, request));
                page.Footer().Element(x => BuildFooter(x, request.SystemName, request.Metadata.GeneratedBy));
            });
        }).GeneratePdf();
    }

    public static string FormatMoney(decimal value) => value.ToString("N2");

    public static string FormatDate(DateTime value) => value.ToString("yyyy-MM-dd HH:mm");

    public static string MapColumnLabel(string key)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["date"] = "Fecha",
            ["created_at"] = "Fecha",
            ["reference"] = "Referencia",
            ["income"] = "Ingreso",
            ["expense"] = "Egreso",
            ["balance"] = "Saldo",
            ["product_name"] = "Producto",
            ["ticket_number"] = "Ticket",
            ["payment_method"] = "Pago",
            ["stock_quantity"] = "Stock",
            ["cost_price"] = "Costo",
            ["sale_price"] = "Precio",
            ["total"] = "Total",
            ["amount"] = "Monto",
            ["category"] = "Categoría"
        };
        if (map.TryGetValue(key, out var label)) return label;
        return key.Replace("_", " ").Trim();
    }

    private static void BuildHeader(IContainer container, PdfReportRequest request, byte[]? logo)
    {
        container.Background(HeaderBg).Padding(8).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(130).Height(40).AlignLeft().Element(x =>
                {
                    if (logo is null)
                    {
                        x.Border(1).BorderColor(Line).AlignMiddle().AlignCenter().Text(request.SystemName).FontSize(8).FontColor(Muted);
                        return;
                    }

                    x.Image(logo).FitArea();
                });

                row.RelativeItem().AlignCenter().Column(c =>
                {
                    c.Item().AlignCenter().Text(request.SystemName).FontSize(14).Bold().FontColor(Text);
                    c.Item().AlignCenter().Text(request.Title).FontSize(12).SemiBold().FontColor(Accent);
                });

                row.ConstantItem(160).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"Fecha: {FormatDate(request.Metadata.GeneratedAt)}").FontSize(8).FontColor(Muted);
                    c.Item().AlignRight().Text(t =>
                    {
                        t.Span("Página ").FontSize(8).FontColor(Muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                        t.Span(" de ").FontSize(8).FontColor(Muted);
                        t.TotalPages().FontSize(8).FontColor(Muted);
                    });
                });
            });

            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Line);
        });
    }

    private static void BuildContent(IContainer container, PdfReportRequest request)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().Element(x => BuildMetadata(x, request));

            if (request.SummaryCards.Count > 0)
                col.Item().PaddingTop(8).Element(x => BuildSummaryCards(x, request.SummaryCards));

            if (request.Columns.Count > 0)
                col.Item().PaddingTop(10).Element(x => BuildTable(x, request.Columns, request.Rows, request.TotalsBar));
        });
    }

    private static void BuildMetadata(IContainer container, PdfReportRequest request)
    {
        container.Border(1).BorderColor(Line).Padding(6).Column(col =>
        {
            var period = request.Start.HasValue && request.End.HasValue
                ? $"{request.Start:yyyy-MM-dd} - {request.End:yyyy-MM-dd}"
                : "-";

            col.Item().Row(r =>
            {
                r.RelativeItem().Column(left =>
                {
                    left.Item().Text($"Módulo: {request.ModuleName}").FontSize(8).FontColor(Muted);
                    left.Item().Text($"Período: {period}").FontSize(8).FontColor(Muted);
                    left.Item().Text($"Sucursal/Tienda: {request.Metadata.BranchOrStore}").FontSize(8).FontColor(Muted);
                });
                r.RelativeItem().Column(right =>
                {
                    right.Item().AlignRight().Text($"Generado por: {request.Metadata.GeneratedBy}").FontSize(8).FontColor(Muted);
                    right.Item().AlignRight().Text($"Rol: {request.Metadata.Role}").FontSize(8).FontColor(Muted);
                    right.Item().AlignRight().Text($"Sistema: {request.SystemName}").FontSize(8).FontColor(Muted);
                });
            });
            col.Item().PaddingTop(3).Text($"Filtros aplicados: {request.Metadata.Filters}").FontSize(8).FontColor(Muted);
        });
    }

    private static void BuildSummaryCards(IContainer container, IReadOnlyList<PdfSummaryCard> cards)
    {
        container.Row(row =>
        {
            foreach (var card in cards)
            {
                row.RelativeItem().PaddingRight(6).Border(1).BorderColor(Line).Background(CardBg).Padding(8).Column(c =>
                {
                    c.Item().AlignCenter().Text(card.Title).FontSize(8).FontColor(Muted);
                    c.Item().AlignCenter().PaddingTop(2).Text(card.Value).FontSize(12).Bold();
                });
            }
        });
    }

    private static void BuildTable(IContainer container, IReadOnlyList<PdfColumnDefinition> columns, IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<PdfTotalsBar>? totalsBar)
    {
        container.Column(col =>
        {
            col.Item().Background("#FFFFFF").Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    foreach (var _ in columns) c.RelativeColumn();
                });

                foreach (var col in columns)
                {
                    table.Cell().Background(HeaderBg).Border(1).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(6).Element(x =>
                    {
                        var text = x.Text(col.Label).FontSize(9).SemiBold();
                        if (col.Alignment == PdfCellAlignment.Right) text.AlignRight();
                        else if (col.Alignment == PdfCellAlignment.Center) text.AlignCenter();
                        else text.AlignLeft();
                    });
                }

                for (var i = 0; i < rows.Count; i++)
                {
                    var rowBg = i % 2 == 1 ? Zebra : "#FFFFFF";
                    for (var j = 0; j < columns.Count; j++)
                    {
                        var value = j < rows[i].Count ? rows[i][j] : string.Empty;
                        var colDef = columns[j];
                        table.Cell().Background(rowBg).Border(1).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(6).Element(x =>
                        {
                            var text = x.Text(value ?? string.Empty).FontSize(9);
                            if (colDef.Alignment == PdfCellAlignment.Right) text.AlignRight();
                            else if (colDef.Alignment == PdfCellAlignment.Center) text.AlignCenter();
                            else text.AlignLeft();
                        });
                    }
                }
            });

            if (totalsBar is { Count: > 0 })
            {
                col.Item().PaddingTop(6).Background("#EAF2FF").Border(1).BorderColor(Line).Padding(8).Row(r =>
                {
                    foreach (var item in totalsBar)
                    {
                        r.RelativeItem().AlignCenter().Text($"{item.Label}: {item.Value}").SemiBold().FontSize(9).FontColor(Text);
                    }
                });
            }
        });
    }

    private static void BuildFooter(IContainer container, string systemName, string generatedBy)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Line);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"{systemName} v1.0 | Powered by America PRO © 2026").FontSize(8).FontColor(Muted);
                row.RelativeItem().AlignCenter().Text($"Generado por: {generatedBy}").FontSize(8).FontColor(Muted);
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(8).FontColor(Muted);
                    x.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    x.Span(" of ").FontSize(8).FontColor(Muted);
                    x.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        });
    }

    private static byte[]? TryLoadLogo()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "assets", "logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "client", "public", "assets", "logo.png"),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "logo_lb.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "client", "public", "logo_lb.png")
        };

        var file = candidates.FirstOrDefault(File.Exists);
        if (file is null) return null;
        try { return File.ReadAllBytes(file); } catch { return null; }
    }
}
