using LBElectronica.Server.Data;
using LBElectronica.Server.DTOs;
using LBElectronica.Server.Models;
using LBElectronica.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Endpoints;

public static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystem(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system").RequireAuthorization();

        group.MapGet("/dashboard", async (AppDbContext db, HttpContext ctx) =>
        {
            var todayStart = DateTime.Now.Date;
            var todayEnd = todayStart.AddDays(1).AddTicks(-1);

            if (ctx.User.IsAdmin())
            {
                var todaySales = await db.Sales.Where(x => x.Date >= todayStart && x.Date <= todayEnd).SumAsync(x => (decimal?)x.Total) ?? 0;
                var expenses = await db.CashMovements.Where(x => x.CreatedAt >= todayStart && x.CreatedAt <= todayEnd && x.Type == CashMovementType.Expense).SumAsync(x => (decimal?)x.Amount) ?? 0;
                var gross = await db.SaleItems.Include(x => x.Sale).Where(x => x.Sale!.Date >= todayStart && x.Sale.Date <= todayEnd)
                    .SumAsync(x => (decimal?)((x.SalePriceSnapshot - x.CostPriceSnapshot) * x.Qty)) ?? 0;

                return Results.Ok(new
                {
                    role = "Admin",
                    todaySales,
                    grossProfit = gross,
                    expenses,
                    netProfit = gross - expenses,
                    lowStockCount = await db.Products.CountAsync(x => x.StockQuantity <= x.StockMinimum)
                });
            }

            var uid = ctx.User.UserId();
            var mySales = await db.Sales.Where(x => x.UserId == uid && x.Date >= todayStart && x.Date <= todayEnd).SumAsync(x => (decimal?)x.Total) ?? 0;
            var openSession = await db.CashSessions.Include(x => x.Movements).FirstOrDefaultAsync(x => x.UserId == uid && x.IsOpen);
            var expected = 0m;
            if (openSession is not null)
            {
                var incomes = openSession.Movements
                    .Where(x => x.Type == CashMovementType.Income &&
                                (x.Category == null || !x.Category.StartsWith("VENTA:") || x.Category == "VENTA:Cash"))
                    .Sum(x => x.Amount);
                var expenses = openSession.Movements.Where(x => x.Type == CashMovementType.Expense).Sum(x => x.Amount);
                expected = openSession.OpeningAmount + incomes - expenses;
            }

            return Results.Ok(new
            {
                role = "Cashier",
                todaySales = mySales,
                hasOpenCashSession = openSession is not null,
                expectedCash = expected
            });
        });

        group.MapPost("/backup", async (IConfiguration config, HttpContext ctx) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();

            var dbPath = config.GetConnectionString("Default")?.Replace("Data Source=", "").Trim() ?? "lb_electronica.db";
            if (!Path.IsPathRooted(dbPath)) dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);

            if (!File.Exists(dbPath)) return Results.BadRequest(new { message = "No se encontró el archivo de base de datos" });

            var backupDir = Path.Combine(AppContext.BaseDirectory, "backups");
            Directory.CreateDirectory(backupDir);
            var backupFile = Path.Combine(backupDir, $"lb_electronica_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            File.Copy(dbPath, backupFile, true);

            return Results.Ok(new { file = backupFile });
        });

        group.MapGet("/exchange-rate", async (AppDbContext db) =>
        {
            var rate = await db.ExchangeRates
                .AsNoTracking()
                .OrderByDescending(x => x.EffectiveDate)
                .Select(x => new { x.Id, x.ArsPerUsd, x.EffectiveDate, x.CreatedAt, x.UserId })
                .FirstOrDefaultAsync();
            if (rate is null) return Results.Ok(new { ArsPerUsd = 1m });
            return Results.Ok(rate);
        });

        group.MapPost("/exchange-rate", async (ExchangeRateRequest request, AppDbContext db, HttpContext ctx, AuditService auditService) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();
            if (request.ArsPerUsd <= 0) return Results.BadRequest(new { message = "Cotización inválida" });

            var rate = new ExchangeRate
            {
                ArsPerUsd = request.ArsPerUsd,
                EffectiveDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UserId = ctx.User.UserId()
            };
            db.ExchangeRates.Add(rate);
            await db.SaveChangesAsync();
            await auditService.LogAsync(ctx.User.UserId(), "EXCHANGE_RATE_SET", "ExchangeRate", rate.Id.ToString(), request.ArsPerUsd.ToString());
            return Results.Ok(rate);
        });

        return group;
    }
}
