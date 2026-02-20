using LBElectronica.Server.Models;

namespace LBElectronica.Server.Services;

public class PdfService(PdfReportService engine)
{
    public byte[] SalesSummary(
        string title,
        IEnumerable<(string Label, decimal Amount)> lines,
        DateTime start,
        DateTime end,
        string generatedBy = "Sistema",
        string generatedRole = "-",
        string? filters = null)
    {
        var items = lines.ToList();
        var summary = items.Select(x => new PdfSummaryCard(x.Label, PdfReportService.FormatMoney(x.Amount))).ToList();
        var rows = items.Select(x => (IReadOnlyList<string>)new List<string> { x.Label, PdfReportService.FormatMoney(x.Amount) }).ToList();

        return engine.Generate(new PdfReportRequest(
            SystemName: "LB Electronica",
            ModuleName: "Finance",
            Title: title,
            Metadata: new PdfReportMetadata(generatedBy, generatedRole, DateTime.Now, filters ?? "Preset/Range", "Casa Central"),
            Start: start,
            End: end,
            SummaryCards: summary,
            Columns: new List<PdfColumnDefinition>
            {
                new("label", "Concepto"),
                new("amount", "Monto", PdfCellAlignment.Right)
            },
            Rows: rows,
            ForceLandscape: false
        ));
    }

    public byte[] TableReport(
        string title,
        List<string> headers,
        List<List<string>> rows,
        DateTime start,
        DateTime end,
        string generatedBy = "Sistema",
        string generatedRole = "-",
        string? filters = null,
        string moduleName = "Reports")
    {
        var columns = headers.Select((h, i) =>
        {
            var numeric = IsNumericHeader(h);
            return new PdfColumnDefinition(
                $"col_{i}",
                string.IsNullOrWhiteSpace(h) ? PdfReportService.MapColumnLabel($"col_{i}") : h,
                numeric ? PdfCellAlignment.Right : PdfCellAlignment.Left);
        }).ToList();

        var tableRows = rows.Select(r => (IReadOnlyList<string>)r).ToList();
        var summary = new List<PdfSummaryCard>
        {
            new("Total Records", rows.Count.ToString("N0"))
        };

        return engine.Generate(new PdfReportRequest(
            SystemName: "LB Electronica",
            ModuleName: moduleName,
            Title: title,
            Metadata: new PdfReportMetadata(generatedBy, generatedRole, DateTime.Now, filters ?? "Preset/Range", "Casa Central"),
            Start: start,
            End: end,
            SummaryCards: summary,
            Columns: columns,
            Rows: tableRows
        ));
    }

    public byte[] CashReport(
        DateTime start,
        DateTime end,
        IEnumerable<(DateTime Date, string Reference, decimal Income, decimal Expense)> rows,
        decimal totalIncome,
        decimal totalExpense,
        decimal balance,
        string generatedBy = "Sistema",
        string generatedRole = "-",
        string? filters = null)
    {
        decimal running = 0;
        var tableRows = rows.Select(x =>
        {
            running += x.Income;
            running -= x.Expense;
            return new List<string>
            {
                x.Date.ToString("yyyy-MM-dd HH:mm"),
                x.Reference,
                x.Income > 0 ? PdfReportService.FormatMoney(x.Income) : "-",
                x.Expense > 0 ? PdfReportService.FormatMoney(x.Expense) : "-",
                PdfReportService.FormatMoney(running)
            };
        }).ToList();

        var summary = new List<PdfSummaryCard>
        {
            new("Total Income", PdfReportService.FormatMoney(totalIncome)),
            new("Total Expenses", PdfReportService.FormatMoney(totalExpense)),
            new("Net Balance", PdfReportService.FormatMoney(balance)),
            new("Total Records", tableRows.Count.ToString("N0"))
        };

        return engine.Generate(new PdfReportRequest(
            SystemName: "LB Electronica",
            ModuleName: "Cash",
            Title: "Reporte de Caja",
            Metadata: new PdfReportMetadata(generatedBy, generatedRole, DateTime.Now, filters ?? "Preset/Range", "Casa Central"),
            Start: start,
            End: end,
            SummaryCards: summary,
            Columns: new List<PdfColumnDefinition>
            {
                new("date", "Fecha", PdfCellAlignment.Center),
                new("reference", "Referencia"),
                new("income", "Ingreso", PdfCellAlignment.Right),
                new("expense", "Egreso", PdfCellAlignment.Right),
                new("balance", "Saldo", PdfCellAlignment.Right)
            },
            Rows: tableRows,
            TotalsBar: new List<PdfTotalsBar>
            {
                new("TOTAL INGRESOS", PdfReportService.FormatMoney(totalIncome)),
                new("TOTAL EGRESOS", PdfReportService.FormatMoney(totalExpense)),
                new("SALDO FINAL", PdfReportService.FormatMoney(balance))
            }
        ));
    }

    public byte[] InventoryReport(IEnumerable<Product> products, string generatedBy = "Sistema", string generatedRole = "-", string? filters = null)
    {
        var now = DateTime.Now;
        var items = products.ToList();
        var valuation = items.Sum(x => x.StockQuantity * x.CostPrice);
        var rows = items.Select(p => (IReadOnlyList<string>)new List<string>
        {
            p.InternalCode,
            p.Name,
            p.StockQuantity.ToString("0.##"),
            PdfReportService.FormatMoney(p.StockQuantity * p.CostPrice)
        }).ToList();

        return engine.Generate(new PdfReportRequest(
            SystemName: "LB Electronica",
            ModuleName: "Stock",
            Title: "Inventory Report",
            Metadata: new PdfReportMetadata(generatedBy, generatedRole, DateTime.Now, filters ?? "Current stock", "Casa Central"),
            Start: now.Date,
            End: now,
            SummaryCards: new List<PdfSummaryCard>
            {
                new("Total Records", items.Count.ToString("N0")),
                new("Stock Valuation", PdfReportService.FormatMoney(valuation))
            },
            Columns: new List<PdfColumnDefinition>
            {
                new("code", "Code"),
                new("product", "Product"),
                new("stock", "Stock", PdfCellAlignment.Right),
                new("value", "Value at Cost", PdfCellAlignment.Right)
            },
            Rows: rows
        ));
    }

    private static bool IsNumericHeader(string header)
    {
        var h = header.Trim().ToLowerInvariant();
        return h.Contains("monto")
            || h.Contains("total")
            || h.Contains("precio")
            || h.Contains("costo")
            || h.Contains("ingreso")
            || h.Contains("egreso")
            || h.Contains("cant")
            || h.Contains("amount")
            || h.Contains("qty")
            || h.Contains("value");
    }
}
