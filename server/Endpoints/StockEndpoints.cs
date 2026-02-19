using LBElectronica.Server.Data;
using LBElectronica.Server.DTOs;
using LBElectronica.Server.Models;
using LBElectronica.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Endpoints;

public static class StockEndpoints
{
    public static RouteGroupBuilder MapStock(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock").RequireAuthorization();

        group.MapPost("/entries", async (
            CreateStockEntryRequest request,
            AppDbContext db,
            HttpContext ctx,
            AuditService auditService,
            CodeService codeService,
            ExchangeRateService exchangeRateService) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();
            if (request.Items.Count == 0) return Results.BadRequest(new { message = "Debes agregar al menos un ítem" });

            var configuredRate = await exchangeRateService.GetCurrentRateAsync();
            var rate = request.ExchangeRateArs is > 1m ? request.ExchangeRateArs.Value : configuredRate;
            var userId = ctx.User.UserId();

            await using var tx = await db.Database.BeginTransactionAsync();

            var entry = new StockEntry
            {
                Date = request.Date,
                Supplier = request.Supplier,
                DocumentNumber = request.DocumentNumber,
                Notes = request.Notes,
                LogisticsUsd = request.LogisticsUsd,
                ExchangeRateArs = rate,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            db.StockEntries.Add(entry);
            await db.SaveChangesAsync();

            entry.BatchCode = $"LOTE-{entry.Id:D6}";
            await db.SaveChangesAsync();

            var preparedItems = new List<(Product product, StockEntryItemRequest req)>();
            foreach (var item in request.Items)
            {
                Product? product = null;
                if (item.ProductId.HasValue)
                    product = await db.Products.FindAsync(item.ProductId.Value);

                if (product is null && !string.IsNullOrWhiteSpace(item.ProductName))
                {
                    var normalized = item.ProductName.Trim();
                    product = await db.Products.FirstOrDefaultAsync(x => x.Name.ToLower() == normalized.ToLower());
                    if (product is null)
                    {
                        var nextCode = await codeService.NextProductCodeAsync();
                        var margin = item.MarginPercent ?? 80m;
                        var saleUsd = item.PurchaseUnitCostUsd * (1 + (margin / 100m));
                        product = new Product
                        {
                            InternalCode = nextCode,
                            Name = normalized,
                            Category = item.Category,
                            CostPrice = item.PurchaseUnitCostUsd,
                            MarginPercent = margin,
                            SalePrice = saleUsd,
                            StockQuantity = 0,
                            StockMinimum = 1,
                            Active = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        db.Products.Add(product);
                        await db.SaveChangesAsync();
                    }
                }

                if (product is null)
                    return Results.BadRequest(new { message = "Producto inválido en el lote" });

                preparedItems.Add((product, item));
            }

            var totalPurchaseUsd = preparedItems.Sum(x => x.req.Qty * x.req.PurchaseUnitCostUsd);

            foreach (var (product, item) in preparedItems)
            {
                if (item.Qty <= 0) return Results.BadRequest(new { message = "Cantidad inválida en el lote" });
                if (item.PurchaseUnitCostUsd < 0) return Results.BadRequest(new { message = "Costo USD inválido" });

                var linePurchaseUsd = item.Qty * item.PurchaseUnitCostUsd;
                var logisticsLineUsd = totalPurchaseUsd > 0 ? request.LogisticsUsd * (linePurchaseUsd / totalPurchaseUsd) : 0;
                var logisticsUnitUsd = item.Qty > 0 ? logisticsLineUsd / item.Qty : 0;
                var finalUnitUsd = item.PurchaseUnitCostUsd + logisticsUnitUsd;
                var finalUnitArs = finalUnitUsd * rate;

                var margin = item.MarginPercent ?? product.MarginPercent;
                var saleUsd = finalUnitUsd * (1 + (margin / 100m));

                product.StockQuantity += item.Qty;
                product.CostPrice = finalUnitUsd;
                product.MarginPercent = margin;
                product.SalePrice = saleUsd;
                product.LastStockExchangeRateArs = rate;
                product.UpdatedAt = DateTime.UtcNow;

                db.StockEntryItems.Add(new StockEntryItem
                {
                    StockEntryId = entry.Id,
                    ProductId = product.Id,
                    Qty = item.Qty,
                    CostPrice = finalUnitUsd,
                    PurchaseUnitCostUsd = item.PurchaseUnitCostUsd,
                    LogisticsUnitCostUsd = logisticsUnitUsd,
                    FinalUnitCostUsd = finalUnitUsd,
                    FinalUnitCostArs = finalUnitArs,
                    MarginPercent = margin,
                    SalePriceSnapshot = saleUsd
                });

                db.LedgerMovements.Add(new LedgerMovement
                {
                    MovementType = LedgerMovementType.In,
                    ReferenceType = LedgerReferenceType.Purchase,
                    ProductId = product.Id,
                    ReferenceId = entry.Id,
                    Qty = item.Qty,
                    UnitCost = finalUnitUsd,
                    UnitSalePriceSnapshot = saleUsd,
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await auditService.LogAsync(userId, "STOCK_ENTRY_CREATE", "StockEntry", entry.Id.ToString(), $"Lote {entry.BatchCode} - Items: {request.Items.Count}");

            return Results.Ok(new { entry.Id, entry.BatchCode });
        });

        group.MapPost("/out", async (StockOutRequest request, AppDbContext db, HttpContext ctx, AuditService auditService) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();
            if (request.Qty <= 0) return Results.BadRequest(new { message = "Cantidad inválida" });

            var product = await db.Products.FindAsync(request.ProductId);
            if (product is null) return Results.NotFound();
            if (product.StockQuantity < request.Qty) return Results.BadRequest(new { message = "Stock insuficiente" });

            product.StockQuantity -= request.Qty;
            product.UpdatedAt = DateTime.UtcNow;

            db.LedgerMovements.Add(new LedgerMovement
            {
                MovementType = LedgerMovementType.Out,
                ReferenceType = LedgerReferenceType.ManualAdjust,
                ProductId = product.Id,
                Qty = request.Qty,
                UnitCost = product.CostPrice,
                UnitSalePriceSnapshot = product.SalePrice,
                UserId = ctx.User.UserId(),
                Timestamp = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
            await auditService.LogAsync(ctx.User.UserId(), "STOCK_OUT", "Product", product.Id.ToString(), request.Reason);
            return Results.Ok();
        });

        group.MapPost("/adjust", async (ManualAdjustStockRequest request, AppDbContext db, HttpContext ctx, AuditService auditService) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();

            var product = await db.Products.FindAsync(request.ProductId);
            if (product is null) return Results.NotFound();

            product.StockQuantity += request.Qty;
            product.UpdatedAt = DateTime.UtcNow;

            db.LedgerMovements.Add(new LedgerMovement
            {
                MovementType = LedgerMovementType.Adjust,
                ReferenceType = LedgerReferenceType.ManualAdjust,
                ProductId = product.Id,
                Qty = request.Qty,
                UnitCost = product.CostPrice,
                UnitSalePriceSnapshot = product.SalePrice,
                UserId = ctx.User.UserId(),
                Timestamp = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
            await auditService.LogAsync(ctx.User.UserId(), "STOCK_ADJUST", "Product", product.Id.ToString(), request.Notes);
            return Results.Ok();
        });

        group.MapGet("/entries", async (AppDbContext db) =>
        {
            var entriesRaw = await db.StockEntries
                .AsNoTracking()
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .OrderByDescending(x => x.Date)
                .Take(200)
                .ToListAsync();

            var entries = entriesRaw.Select(x => new
            {
                x.Id,
                batchCode = string.IsNullOrWhiteSpace(x.BatchCode) ? $"LOTE-{x.Id:D6}" : x.BatchCode,
                x.Date,
                x.Supplier,
                x.DocumentNumber,
                x.Notes,
                x.LogisticsUsd,
                x.ExchangeRateArs,
                totalUsd = x.Items.Sum(i => i.FinalUnitCostUsd * i.Qty),
                items = x.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    productName = i.Product?.Name,
                    i.Qty,
                    i.PurchaseUnitCostUsd,
                    i.LogisticsUnitCostUsd,
                    i.FinalUnitCostUsd,
                    i.FinalUnitCostArs,
                    i.MarginPercent,
                    i.SalePriceSnapshot
                })
            }).ToList();
            return Results.Ok(entries);
        });

        group.MapGet("/lots", async (DateTime? startDate, DateTime? endDate, AppDbContext db) =>
        {
            var start = startDate?.Date ?? DateTime.Now.Date.AddDays(-30);
            var end = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Now.Date.AddDays(1).AddTicks(-1);

            var lotsRaw = await db.StockEntries
                .AsNoTracking()
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .Where(x => x.Date >= start && x.Date <= end)
                .OrderByDescending(x => x.Date)
                .ToListAsync();

            var data = lotsRaw.Select(x => new
            {
                x.Id,
                batchCode = string.IsNullOrWhiteSpace(x.BatchCode) ? $"LOTE-{x.Id:D6}" : x.BatchCode,
                x.Date,
                x.Supplier,
                x.DocumentNumber,
                x.LogisticsUsd,
                x.ExchangeRateArs,
                totalItems = x.Items.Count,
                totalQty = x.Items.Sum(i => i.Qty),
                totalCostUsd = x.Items.Sum(i => i.FinalUnitCostUsd * i.Qty),
                totalCostArs = x.Items.Sum(i => i.FinalUnitCostArs * i.Qty),
                items = x.Items.Select(i => new
                {
                    i.ProductId,
                    productName = i.Product?.Name,
                    i.Qty,
                    i.FinalUnitCostUsd,
                    i.FinalUnitCostArs
                })
            }).ToList();

            return Results.Ok(data);
        }).RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

        group.MapGet("/ledger", async (DateTime? startDate, DateTime? endDate, AppDbContext db) =>
        {
            var start = startDate?.Date ?? DateTime.Now.Date.AddDays(-30);
            var end = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Now.Date.AddDays(1).AddTicks(-1);

            var data = await db.LedgerMovements
                .AsNoTracking()
                .Include(x => x.Product)
                .Include(x => x.User)
                .Where(x => x.Timestamp >= start && x.Timestamp <= end)
                .OrderByDescending(x => x.Timestamp)
                .Take(1000)
                .ToListAsync();

            return Results.Ok(data);
        }).RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

        return group;
    }
}
