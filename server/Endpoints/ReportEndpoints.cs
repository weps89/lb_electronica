using LBElectronica.Server.Data;
using LBElectronica.Server.Services;
using LBElectronica.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LBElectronica.Server.Endpoints;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReports(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

        group.MapGet("/income-expense-summary", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);

            var sales = (await db.Sales.Where(x => x.Date >= start && x.Date <= end && (x.Status == SaleStatus.Paid || x.Status == SaleStatus.Verified)).Select(x => x.Total).ToListAsync()).Sum();
            var incomes = (await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.Type == CashMovementType.Income).Select(x => x.Amount).ToListAsync()).Sum();
            var expenses = (await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.Type == CashMovementType.Expense).Select(x => x.Amount).ToListAsync()).Sum();

            return Results.Ok(new
            {
                start,
                end,
                totalSalesIncome = sales,
                totalCashIncomes = incomes,
                totalExpenses = expenses,
                netCashFlow = sales + incomes - expenses
            });
        });

        group.MapGet("/income-expense-detail", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);

            var sales = await db.Sales.Where(x => x.Date >= start && x.Date <= end && (x.Status == SaleStatus.Paid || x.Status == SaleStatus.Verified))
                .Select(x => new { type = "venta", date = x.Date, reference = x.TicketNumber, amount = x.Total })
                .ToListAsync();

            var moves = await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end)
                .Select(x => new { type = x.Type == CashMovementType.Income ? "ingreso" : "gasto", date = x.CreatedAt, reference = x.Reason, amount = x.Amount })
                .ToListAsync();

            return Results.Ok(sales.Concat(moves).OrderByDescending(x => x.date));
        });

        group.MapGet("/profit", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);
            var sales = await db.Sales
                .AsNoTracking()
                .Where(x => x.Date >= start && x.Date <= end && x.Status != SaleStatus.Cancelled)
                .Select(x => new { x.Id, x.Date, x.Total })
                .ToListAsync();
            var saleIds = sales.Select(x => x.Id).ToList();
            var saleItems = await db.SaleItems
                .AsNoTracking()
                .Where(x => saleIds.Contains(x.SaleId))
                .Select(x => new { x.SaleId, x.Qty, x.CostPriceSnapshotArs, x.CostPriceSnapshot })
                .ToListAsync();
            var costMap = saleItems
                .GroupBy(x => x.SaleId)
                .ToDictionary(g => g.Key, g => g.Sum(x => (x.CostPriceSnapshotArs > 0 ? x.CostPriceSnapshotArs : x.CostPriceSnapshot) * x.Qty));
            var expenses = (await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.Type == CashMovementType.Expense).Select(x => x.Amount).ToListAsync()).Sum();

            var capitalRecovered = sales.Sum(s => costMap.GetValueOrDefault(s.Id, 0m));
            var gross = sales.Sum(s => s.Total) - capitalRecovered;
            var net = gross - expenses;

            var daily = sales.GroupBy(x => x.Date.Date).Select(g => new
            {
                date = g.Key,
                capitalRecovered = g.Sum(s => costMap.GetValueOrDefault(s.Id, 0m)),
                grossProfit = g.Sum(s => s.Total - costMap.GetValueOrDefault(s.Id, 0m))
            }).OrderBy(x => x.date);

            return Results.Ok(new { start, end, capitalRecovered, grossProfit = gross, netProfit = net, expenses, daily });
        });

        group.MapGet("/sales", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);
            var sales = await db.Sales.Where(x => x.Date >= start && x.Date <= end).OrderByDescending(x => x.Date).ToListAsync();
            var byPayment = sales.GroupBy(x => x.PaymentMethod.ToString()).ToDictionary(x => x.Key, x => x.Sum(v => v.Total));
            return Results.Ok(new { start, end, total = sales.Sum(x => x.Total), count = sales.Count, byPayment, data = sales });
        });

        group.MapGet("/inventory", async (AppDbContext db) =>
        {
            var products = await db.Products.OrderBy(x => x.Name).ToListAsync();
            return Results.Ok(new
            {
                items = products,
                lowStock = products.Where(x => x.StockQuantity <= x.StockMinimum).ToList(),
                stockValuationCost = products.Sum(x => x.StockQuantity * x.CostPrice)
            });
        });

        group.MapGet("/cash", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);

            var movements = await db.CashMovements
                .AsNoTracking()
                .Where(x => x.CreatedAt >= start && x.CreatedAt <= end)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            var rows = movements.Select(x => new
            {
                x.CreatedAt,
                reference = x.Reason,
                income = x.Type == CashMovementType.Income ? x.Amount : 0m,
                expense = x.Type == CashMovementType.Expense ? x.Amount : 0m,
                category = x.Category
            }).ToList();

            var totalIncome = rows.Sum(x => x.income);
            var totalExpense = rows.Sum(x => x.expense);

            return Results.Ok(new
            {
                start,
                end,
                incomes = rows.Where(x => x.income > 0),
                expenses = rows.Where(x => x.expense > 0),
                totalIncome,
                totalExpense,
                balance = totalIncome - totalExpense
            });
        });

        group.MapGet("/sales-detailed", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);
            var rows = await db.SaleItems
                .AsNoTracking()
                .Include(x => x.Sale)
                .Include(x => x.Product)
                .Where(x => x.Sale!.Date >= start && x.Sale.Date <= end)
                .OrderByDescending(x => x.Sale!.Date)
                .Select(x => new
                {
                    date = x.Sale!.Date,
                    ticket = x.Sale.TicketNumber,
                    product = x.Product!.Name,
                    category = x.Product.Category,
                    qty = x.Qty,
                    unitPrice = x.UnitPrice,
                    discount = x.Discount,
                    total = (x.Qty * x.UnitPrice) - x.Discount
                })
                .ToListAsync();
            return Results.Ok(new { start, end, count = rows.Count, data = rows });
        });

        group.MapGet("/lot/{lotId:int}", async (int lotId, AppDbContext db) =>
        {
            var lot = await db.StockEntries
                .AsNoTracking()
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == lotId);

            if (lot is null) return Results.NotFound();

            return Results.Ok(new
            {
                lot.Id,
                lot.BatchCode,
                lot.Date,
                lot.Supplier,
                lot.DocumentNumber,
                lot.LogisticsUsd,
                lot.ExchangeRateArs,
                items = lot.Items.Select(i => new
                {
                    product = i.Product!.Name,
                    category = i.Product!.Category,
                    i.Qty,
                    i.FinalUnitCostUsd,
                    i.FinalUnitCostArs
                })
            });
        });

        group.MapGet("/annulments", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);

            var cancelledSales = await db.Sales
                .AsNoTracking()
                .Where(x => x.CancelledAt != null && x.CancelledAt >= start && x.CancelledAt <= end)
                .Select(x => new { date = x.CancelledAt!.Value, type = "ANULACION_VENTA", reference = x.TicketNumber, reason = x.CancelledReason, qty = 0m })
                .ToListAsync();

            var stockAdjust = await db.AuditLogs
                .AsNoTracking()
                .Where(x => x.CreatedAt >= start && x.CreatedAt <= end && (x.Action == "STOCK_OUT" || x.Action == "STOCK_ADJUST"))
                .Select(x => new { date = x.CreatedAt, type = x.Action, reference = x.EntityId ?? "-", reason = x.Details, qty = 0m })
                .ToListAsync();

            var data = cancelledSales.Concat(stockAdjust).OrderByDescending(x => x.date).ToList();
            return Results.Ok(new { start, end, count = data.Count, data });
        });

        group.MapGet("/utilities-monthly", async (int? year, int? month, AppDbContext db) =>
        {
            var now = DateTime.Now;
            var y = year ?? now.Year;
            var m = month ?? now.Month;
            var start = new DateTime(y, m, 1);
            var endOfMonth = start.AddMonths(1).AddTicks(-1);
            var endOfToday = now.Date.AddDays(1).AddTicks(-1);
            var end = (y == now.Year && m == now.Month) ? endOfToday : endOfMonth;

            var sales = await db.Sales
                .AsNoTracking()
                .Where(x => x.Date >= start && x.Date <= end && x.Status != SaleStatus.Cancelled)
                .Select(x => new { x.Id, x.Total })
                .ToListAsync();
            var saleIds = sales.Select(x => x.Id).ToList();
            var saleItems = await db.SaleItems
                .AsNoTracking()
                .Where(x => saleIds.Contains(x.SaleId))
                .Select(x => new { x.SaleId, x.Qty, x.CostPriceSnapshotArs, x.CostPriceSnapshot })
                .ToListAsync();
            var costMap = saleItems
                .GroupBy(x => x.SaleId)
                .ToDictionary(g => g.Key, g => g.Sum(x => (x.CostPriceSnapshotArs > 0 ? x.CostPriceSnapshotArs : x.CostPriceSnapshot) * x.Qty));
            var capitalRecovered = sales.Sum(s => costMap.GetValueOrDefault(s.Id, 0m));
            var gross = sales.Sum(s => s.Total) - capitalRecovered;
            var expenses = (await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.Type == CashMovementType.Expense).Select(x => x.Amount).ToListAsync()).Sum();
            return Results.Ok(new { year = y, month = m, capitalRecovered, grossProfit = gross, expenses, netProfit = gross - expenses });
        });

        group.MapGet("/income-expense-summary/pdf", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService, PdfService pdf, HttpContext ctx) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);
            var sales = (await db.Sales.Where(x => x.Date >= start && x.Date <= end && (x.Status == SaleStatus.Paid || x.Status == SaleStatus.Verified)).Select(x => x.Total).ToListAsync()).Sum();
            var incomes = (await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.Type == CashMovementType.Income).Select(x => x.Amount).ToListAsync()).Sum();
            var expenses = (await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.Type == CashMovementType.Expense).Select(x => x.Amount).ToListAsync()).Sum();
            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";

            var bytes = pdf.SalesSummary("Resumen de Ingresos/Gastos", new List<(string Label, decimal Amount)>
            {
                ("Ingresos por ventas", sales),
                ("Ingresos de caja", incomes),
                ("Gastos", expenses),
                ("Flujo neto", sales + incomes - expenses)
            }, start, end, generatedBy, role, $"preset={preset ?? "-"}");

            return Results.File(bytes, "application/pdf", "resumen-ingresos-gastos.pdf");
        });

        group.MapGet("/income-expense-detail/pdf", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService, PdfService pdf, HttpContext ctx) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);
            var sales = await db.Sales.Where(x => x.Date >= start && x.Date <= end)
                .Select(x => new { Date = x.Date, Type = "Venta", Ref = x.TicketNumber, Amount = x.Total })
                .ToListAsync();

            var moves = await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end)
                .Select(x => new { Date = x.CreatedAt, Type = x.Type == CashMovementType.Income ? "Ingreso" : "Gasto", Ref = x.Reason, Amount = x.Amount })
                .ToListAsync();

            var rows = sales.Select(x => new List<string> { x.Date.ToString("dd/MM/yyyy"), x.Type, x.Ref, x.Amount.ToString("N2") })
                .Concat(moves.Select(x => new List<string> { x.Date.ToString("dd/MM/yyyy"), x.Type, x.Ref, x.Amount.ToString("N2") }))
                .OrderByDescending(x => x[0])
                .ToList();

            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var bytes = pdf.TableReport("Detalle de Ingresos/Gastos", new() { "Fecha", "Tipo", "Referencia", "Monto" }, rows, start, end, generatedBy, role, $"preset={preset ?? "-"}", "Finance");
            return Results.File(bytes, "application/pdf", "detalle-ingresos-gastos.pdf");
        });

        group.MapGet("/profit/pdf", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService, PdfService pdf, HttpContext ctx) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);
            var sales = await db.Sales
                .AsNoTracking()
                .Where(x => x.Date >= start && x.Date <= end && x.Status != SaleStatus.Cancelled)
                .OrderBy(x => x.Date)
                .Select(x => new { x.Id, x.Date, x.TicketNumber, x.Total })
                .ToListAsync();
            var saleIds = sales.Select(x => x.Id).ToList();
            var saleItems = await db.SaleItems
                .AsNoTracking()
                .Where(x => saleIds.Contains(x.SaleId))
                .Select(x => new { x.SaleId, x.Qty, x.CostPriceSnapshotArs, x.CostPriceSnapshot })
                .ToListAsync();
            var costMap = saleItems
                .GroupBy(x => x.SaleId)
                .ToDictionary(g => g.Key, g => g.Sum(x => (x.CostPriceSnapshotArs > 0 ? x.CostPriceSnapshotArs : x.CostPriceSnapshot) * x.Qty));
            var expenses = (await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.Type == CashMovementType.Expense).Select(x => x.Amount).ToListAsync()).Sum();
            var capitalRecovered = sales.Sum(s => costMap.GetValueOrDefault(s.Id, 0m));
            var gross = sales.Sum(s => s.Total) - capitalRecovered;
            var net = gross - expenses;

            var rows = sales.Select(s => new List<string>
            {
                s.Date.ToString("dd/MM/yyyy"),
                s.TicketNumber,
                s.Total.ToString("N2"),
                (s.Total - costMap.GetValueOrDefault(s.Id, 0m)).ToString("N2")
            }).ToList();

            rows.Add(new() { "", "Utilidad bruta", "", gross.ToString("N2") });
            rows.Add(new() { "", "Capital recuperado", "", capitalRecovered.ToString("N2") });
            rows.Add(new() { "", "Gastos", "", expenses.ToString("N2") });
            rows.Add(new() { "", "Utilidad neta", "", net.ToString("N2") });

            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var bytes = pdf.TableReport("Reporte de Utilidad", new() { "Fecha", "Ticket", "Venta Neta", "Utilidad" }, rows, start, end, generatedBy, role, $"preset={preset ?? "-"}", "Finance");
            return Results.File(bytes, "application/pdf", "reporte-utilidad.pdf");
        });

        group.MapGet("/sales/pdf", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService, PdfService pdf, HttpContext ctx) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);
            var sales = await db.Sales.Where(x => x.Date >= start && x.Date <= end).OrderByDescending(x => x.Date).ToListAsync();
            var rows = sales.Select(x => new List<string> { x.Date.ToString("dd/MM/yyyy HH:mm"), x.TicketNumber, x.PaymentMethod.ToString(), x.Total.ToString("N2") }).ToList();
            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var bytes = pdf.TableReport("Reporte de Ventas", new() { "Fecha", "Ticket", "Pago", "Total" }, rows, start, end, generatedBy, role, $"preset={preset ?? "-"}", "Sales");
            return Results.File(bytes, "application/pdf", "reporte-ventas.pdf");
        });

        group.MapGet("/inventory/pdf", async (AppDbContext db, PdfService pdf, HttpContext ctx) =>
        {
            var products = await db.Products.OrderBy(x => x.Name).ToListAsync();
            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var bytes = pdf.InventoryReport(products, generatedBy, role, "current=true");
            return Results.File(bytes, "application/pdf", "reporte-inventario.pdf");
        });

        group.MapGet("/cash/pdf", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService, PdfService pdf, HttpContext ctx) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);
            var movements = await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end).OrderBy(x => x.CreatedAt).ToListAsync();
            var rows = movements.Select(x => (x.CreatedAt, x.Reason, x.Type == CashMovementType.Income ? x.Amount : 0m, x.Type == CashMovementType.Expense ? x.Amount : 0m)).ToList();
            var totalIncome = rows.Sum(x => x.Item3);
            var totalExpense = rows.Sum(x => x.Item4);
            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var bytes = pdf.CashReport(start, end, rows, totalIncome, totalExpense, totalIncome - totalExpense, generatedBy, role, $"preset={preset ?? "-"}");
            return Results.File(bytes, "application/pdf", "reporte-caja.pdf");
        });

        group.MapGet("/sales-detailed/pdf", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService, PdfService pdf, HttpContext ctx) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);
            var rows = await db.SaleItems.Include(x => x.Sale).Include(x => x.Product)
                .Where(x => x.Sale!.Date >= start && x.Sale.Date <= end)
                .OrderByDescending(x => x.Sale!.Date)
                .Select(x => new List<string>
                {
                    x.Sale!.Date.ToString("dd/MM/yyyy HH:mm"),
                    x.Sale.TicketNumber,
                    x.Product!.Name,
                    x.Product.Category ?? "-",
                    x.Qty.ToString("0.##"),
                    x.UnitPrice.ToString("N2"),
                    ((x.Qty * x.UnitPrice) - x.Discount).ToString("N2")
                })
                .ToListAsync();
            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var bytes = pdf.TableReport("Reporte Detallado de Ventas", new() { "Fecha", "Ticket", "Producto", "Categoría", "Cant.", "Precio", "Total" }, rows, start, end, generatedBy, role, $"preset={preset ?? "-"}", "Sales");
            return Results.File(bytes, "application/pdf", "reporte-ventas-detallado.pdf");
        });

        group.MapGet("/lot/{lotId:int}/pdf", async (int lotId, AppDbContext db, PdfService pdf, HttpContext ctx) =>
        {
            var lot = await db.StockEntries.Include(x => x.Items).ThenInclude(x => x.Product).FirstOrDefaultAsync(x => x.Id == lotId);
            if (lot is null) return Results.NotFound();
            var batchCode = string.IsNullOrWhiteSpace(lot.BatchCode) ? $"LOTE-{lot.Id:D6}" : lot.BatchCode;
            var rows = lot.Items.Select(i => new List<string>
            {
                i.Product?.Name ?? "-",
                i.Product?.Category ?? "-",
                i.Qty.ToString("0.##"),
                i.FinalUnitCostUsd.ToString("N2"),
                i.FinalUnitCostArs.ToString("N2")
            }).ToList();
            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var bytes = pdf.TableReport($"Reporte de Lote {batchCode}", new() { "Producto", "Categoría", "Cantidad", "Costo Final USD", "Costo Final ARS" }, rows, lot.Date, lot.Date, generatedBy, role, $"lot={batchCode}", "Stock");
            return Results.File(bytes, "application/pdf", $"reporte-lote-{batchCode}.pdf");
        });

        group.MapGet("/annulments/pdf", async (DateTime? startDate, DateTime? endDate, string? preset, AppDbContext db, DateRangeService dateRangeService, PdfService pdf, HttpContext ctx) =>
        {
            var (start, end) = dateRangeService.Resolve(startDate, endDate, preset);

            var cancelledSales = await db.Sales
                .Where(x => x.CancelledAt != null && x.CancelledAt >= start && x.CancelledAt <= end)
                .Select(x => new List<string> { x.CancelledAt!.Value.ToString("dd/MM/yyyy HH:mm"), "ANULACION_VENTA", x.TicketNumber, x.CancelledReason ?? "-" })
                .ToListAsync();

            var stockAdjust = await db.AuditLogs
                .Where(x => x.CreatedAt >= start && x.CreatedAt <= end && (x.Action == "STOCK_OUT" || x.Action == "STOCK_ADJUST"))
                .Select(x => new List<string> { x.CreatedAt.ToString("dd/MM/yyyy HH:mm"), x.Action, x.EntityId ?? "-", x.Details ?? "-" })
                .ToListAsync();

            var rows = cancelledSales.Concat(stockAdjust).OrderByDescending(x => x[0]).ToList();
            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var bytes = pdf.TableReport("Reporte de Anulaciones", new() { "Fecha", "Tipo", "Referencia", "Motivo" }, rows, start, end, generatedBy, role, $"preset={preset ?? "-"}", "Finance");
            return Results.File(bytes, "application/pdf", "reporte-anulaciones.pdf");
        });

        group.MapGet("/utilities-monthly/pdf", async (int? year, int? month, AppDbContext db, PdfService pdf, HttpContext ctx) =>
        {
            var now = DateTime.Now;
            var y = year ?? now.Year;
            var m = month ?? now.Month;
            var start = new DateTime(y, m, 1);
            var endOfMonth = start.AddMonths(1).AddTicks(-1);
            var endOfToday = now.Date.AddDays(1).AddTicks(-1);
            var end = (y == now.Year && m == now.Month) ? endOfToday : endOfMonth;

            var sales = await db.Sales
                .AsNoTracking()
                .Where(x => x.Date >= start && x.Date <= end && x.Status != SaleStatus.Cancelled)
                .OrderBy(x => x.Date)
                .Select(x => new { x.Id, x.Date, x.TicketNumber, x.Total })
                .ToListAsync();
            var saleIds = sales.Select(x => x.Id).ToList();
            var saleItems = await db.SaleItems
                .AsNoTracking()
                .Where(x => saleIds.Contains(x.SaleId))
                .Select(x => new { x.SaleId, x.Qty, x.CostPriceSnapshotArs, x.CostPriceSnapshot })
                .ToListAsync();
            var costMap = saleItems
                .GroupBy(x => x.SaleId)
                .ToDictionary(g => g.Key, g => g.Sum(x => (x.CostPriceSnapshotArs > 0 ? x.CostPriceSnapshotArs : x.CostPriceSnapshot) * x.Qty));
            var capitalRecovered = sales.Sum(s => costMap.GetValueOrDefault(s.Id, 0m));
            var gross = sales.Sum(s => s.Total) - capitalRecovered;
            var expenses = (await db.CashMovements.Where(x => x.CreatedAt >= start && x.CreatedAt <= end && x.Type == CashMovementType.Expense).Select(x => x.Amount).ToListAsync()).Sum();
            var rows = sales.Select(s => new List<string>
            {
                s.Date.ToString("dd/MM/yyyy"),
                s.TicketNumber,
                s.Total.ToString("N2"),
                (s.Total - costMap.GetValueOrDefault(s.Id, 0m)).ToString("N2")
            }).ToList();
            rows.Add(new() { "", "UTILIDAD BRUTA", "", gross.ToString("N2") });
            rows.Add(new() { "", "CAPITAL RECUPERADO", "", capitalRecovered.ToString("N2") });
            rows.Add(new() { "", "GASTOS", "", expenses.ToString("N2") });
            rows.Add(new() { "", "UTILIDAD NETA", "", (gross - expenses).ToString("N2") });
            var generatedBy = ctx.User.Identity?.Name ?? "admin";
            var role = ctx.User.FindFirstValue(ClaimTypes.Role) ?? "-";
            var bytes = pdf.TableReport("Reporte de Utilidades (Mensual)", new() { "Fecha", "Ticket", "Venta Neta", "Utilidad" }, rows, start, end, generatedBy, role, $"year={y},month={m}", "Finance");
            return Results.File(bytes, "application/pdf", "reporte-utilidades-mensual.pdf");
        });

        return group;
    }
}
