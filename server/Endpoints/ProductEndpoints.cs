using LBElectronica.Server.Data;
using LBElectronica.Server.DTOs;
using LBElectronica.Server.Models;
using LBElectronica.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Endpoints;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").RequireAuthorization();

        group.MapGet("/", async (string? q, bool? active, AppDbContext db, HttpContext ctx, ExchangeRateService exchangeRateService) =>
        {
            var query = db.Products.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(term) || x.InternalCode.ToLower().Contains(term) || (x.Barcode != null && x.Barcode.Contains(term)));
            }

            if (active.HasValue)
                query = query.Where(x => x.Active == active.Value);

            var isAdmin = ctx.User.IsAdmin();
            var arsRate = await exchangeRateService.GetCurrentRateAsync();
            var data = await query.OrderBy(x => x.Name).Take(500).ToListAsync();

            if (isAdmin)
            {
                return Results.Ok(data.Select(x => new
                {
                    x.Id,
                    x.InternalCode,
                    x.Barcode,
                    x.Name,
                    x.Category,
                    x.Brand,
                    x.Model,
                    x.ImeiOrSerial,
                    x.CostPrice,
                    x.MarginPercent,
                    x.SalePrice,
                    PriceCashArs = PricingService.FinalArs(x, PaymentMethod.Cash, arsRate),
                    PriceCardArs = PricingService.FinalArs(x, PaymentMethod.Card, arsRate),
                    PriceTransferArs = PricingService.FinalArs(x, PaymentMethod.Transfer, arsRate),
                    x.StockQuantity,
                    x.StockMinimum,
                    x.Active,
                    x.CreatedAt,
                    x.UpdatedAt
                }));
            }

            return Results.Ok(data.Select(x => new
            {
                x.Id,
                x.InternalCode,
                x.Barcode,
                x.Name,
                x.Category,
                x.Brand,
                x.Model,
                x.ImeiOrSerial,
                SalePrice = PricingService.FinalArs(x, PaymentMethod.Cash, arsRate),
                PriceCashArs = PricingService.FinalArs(x, PaymentMethod.Cash, arsRate),
                PriceCardArs = PricingService.FinalArs(x, PaymentMethod.Card, arsRate),
                PriceTransferArs = PricingService.FinalArs(x, PaymentMethod.Transfer, arsRate),
                x.StockQuantity,
                x.StockMinimum,
                x.Active,
                x.CreatedAt,
                x.UpdatedAt
            }));
        });

        group.MapGet("/{id:int}", async (int id, AppDbContext db, HttpContext ctx, ExchangeRateService exchangeRateService) =>
        {
            var p = await db.Products.FindAsync(id);
            if (p is null) return Results.NotFound();
            var arsRate = await exchangeRateService.GetCurrentRateAsync();

            if (ctx.User.IsAdmin()) return Results.Ok(new
            {
                p.Id,
                p.InternalCode,
                p.Barcode,
                p.Name,
                p.Category,
                p.Brand,
                p.Model,
                p.ImeiOrSerial,
                p.CostPrice,
                p.MarginPercent,
                p.SalePrice,
                PriceCashArs = PricingService.FinalArs(p, PaymentMethod.Cash, arsRate),
                PriceCardArs = PricingService.FinalArs(p, PaymentMethod.Card, arsRate),
                PriceTransferArs = PricingService.FinalArs(p, PaymentMethod.Transfer, arsRate),
                p.StockQuantity,
                p.StockMinimum,
                p.Active,
                p.CreatedAt,
                p.UpdatedAt
            });
            return Results.Ok(new
            {
                p.Id,
                p.InternalCode,
                p.Barcode,
                p.Name,
                p.Category,
                p.Brand,
                p.Model,
                p.ImeiOrSerial,
                SalePrice = PricingService.FinalArs(p, PaymentMethod.Cash, arsRate),
                PriceCashArs = PricingService.FinalArs(p, PaymentMethod.Cash, arsRate),
                PriceCardArs = PricingService.FinalArs(p, PaymentMethod.Card, arsRate),
                PriceTransferArs = PricingService.FinalArs(p, PaymentMethod.Transfer, arsRate),
                p.StockQuantity,
                p.StockMinimum,
                p.Active,
                p.CreatedAt,
                p.UpdatedAt
            });
        });

        group.MapPost("/", async (ProductUpsertRequest request, AppDbContext db, CodeService codeService, HttpContext ctx, AuditService auditService, ExchangeRateService exchangeRateService) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();

            if (!string.IsNullOrWhiteSpace(request.Barcode) && await db.Products.AnyAsync(x => x.Barcode == request.Barcode))
                return Results.BadRequest(new { message = "El código de barras ya existe" });

            var cost = request.CostPrice ?? 0;
            var margin = request.MarginPercent ?? 0;
            var salePrice = request.SalePrice ?? (cost * (1 + (margin / 100m)));

            var product = new Product
            {
                InternalCode = await codeService.NextProductCodeAsync(),
                Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
                Name = request.Name.Trim(),
                Category = request.Category,
                Brand = request.Brand,
                Model = request.Model,
                ImeiOrSerial = request.ImeiOrSerial,
                CostPrice = cost,
                MarginPercent = margin,
                SalePrice = salePrice,
                LastStockExchangeRateArs = await exchangeRateService.GetCurrentRateAsync(),
                StockQuantity = request.StockQuantity,
                StockMinimum = request.StockMinimum,
                Active = request.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();
            await auditService.LogAsync(ctx.User.UserId(), "PRODUCT_CREATE", "Product", product.Id.ToString(), product.Name);
            return Results.Ok(product);
        });

        group.MapPut("/{id:int}", async (int id, ProductUpsertRequest request, AppDbContext db, HttpContext ctx, AuditService auditService) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();
            var product = await db.Products.FindAsync(id);
            if (product is null) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(request.Barcode) && await db.Products.AnyAsync(x => x.Barcode == request.Barcode && x.Id != id))
                return Results.BadRequest(new { message = "El código de barras ya existe" });

            var oldCost = product.CostPrice;
            var oldMargin = product.MarginPercent;
            product.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
            product.Name = request.Name.Trim();
            product.Category = request.Category;
            product.Brand = request.Brand;
            product.Model = request.Model;
            product.ImeiOrSerial = request.ImeiOrSerial;
            product.CostPrice = request.CostPrice ?? product.CostPrice;
            product.MarginPercent = request.MarginPercent ?? product.MarginPercent;
            product.SalePrice = request.SalePrice ?? (product.CostPrice * (1 + (product.MarginPercent / 100m)));
            product.StockQuantity = request.StockQuantity;
            product.StockMinimum = request.StockMinimum;
            product.Active = request.Active;
            product.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            if (oldCost != product.CostPrice || oldMargin != product.MarginPercent)
            {
                await auditService.LogAsync(ctx.User.UserId(), "PRODUCT_COST_MARGIN_CHANGE", "Product", product.Id.ToString(),
                    $"Cost {oldCost}=>{product.CostPrice}, Margin {oldMargin}=>{product.MarginPercent}");
            }

            return Results.Ok(product);
        });

        group.MapPost("/manual-adjust", async (ManualAdjustStockRequest request, AppDbContext db, HttpContext ctx, AuditService auditService) =>
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
            await auditService.LogAsync(ctx.User.UserId(), "STOCK_MANUAL_ADJUST", "Product", product.Id.ToString(), request.Notes);

            return Results.Ok(product);
        });

        return group;
    }
}
