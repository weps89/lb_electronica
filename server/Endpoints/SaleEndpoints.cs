using System.Text.Encodings.Web;
using LBElectronica.Server.Data;
using LBElectronica.Server.DTOs;
using LBElectronica.Server.Models;
using LBElectronica.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Endpoints;

public static class SaleEndpoints
{
    public static RouteGroupBuilder MapSales(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales").RequireAuthorization();

        group.MapPost("/", async (CreateSaleRequest request, AppDbContext db, HttpContext ctx, CodeService codeService, AuditService auditService) =>
        {
            if (request.Items.Count == 0) return Results.BadRequest(new { message = "El carrito no puede estar vacío" });
            if (!Enum.IsDefined(typeof(PaymentMethod), request.PaymentMethod))
                return Results.BadRequest(new { message = "Método de pago inválido" });

            if (request.Items.Any(x => x.Qty <= 0))
                return Results.BadRequest(new { message = "Todas las cantidades deben ser mayores a 0" });

            if (request.Items.Any(x => x.UnitPrice < 0 || x.Discount < 0))
                return Results.BadRequest(new { message = "Precio o descuento inválido" });

            await using var tx = await db.Database.BeginTransactionAsync();

            var arsRate = await db.ExchangeRates
                .OrderByDescending(x => x.EffectiveDate)
                .Select(x => (decimal?)x.ArsPerUsd)
                .FirstOrDefaultAsync() ?? 1m;

            var sale = new Sale
            {
                TicketNumber = await codeService.NextTicketNumberAsync(),
                Date = DateTime.UtcNow,
                UserId = ctx.User.UserId(),
                PaymentMethod = request.PaymentMethod,
                Status = SaleStatus.Pending
            };

            if (request.Customer is not null)
            {
                var maybeCustomer = await UpsertCustomerAsync(request.Customer, db);
                if (maybeCustomer is not null)
                    sale.CustomerId = maybeCustomer.Id;
            }

            decimal subtotal = 0;
            decimal discount = 0;

            var pendingLedger = new List<LedgerMovement>();
            foreach (var item in request.Items)
            {
                var product = await db.Products.FindAsync(item.ProductId);
                if (product is null) return Results.BadRequest(new { message = $"Producto {item.ProductId} no encontrado" });
                if (product.StockQuantity < item.Qty) return Results.BadRequest(new { message = $"Stock insuficiente para {product.Name}" });

                product.StockQuantity -= item.Qty;
                product.UpdatedAt = DateTime.UtcNow;

                var unitPriceArs = PricingService.FinalArs(product, request.PaymentMethod, arsRate);
                var rowSubtotal = unitPriceArs * item.Qty;
                subtotal += rowSubtotal;
                discount += item.Discount;

                sale.Items.Add(new SaleItem
                {
                    ProductId = product.Id,
                    Qty = item.Qty,
                    UnitPrice = unitPriceArs,
                    Discount = item.Discount,
                    CostPriceSnapshot = product.CostPrice,
                    SalePriceSnapshot = unitPriceArs,
                    ImeiOrSerial = item.ImeiOrSerial
                });

                pendingLedger.Add(new LedgerMovement
                {
                    MovementType = LedgerMovementType.Out,
                    ReferenceType = LedgerReferenceType.Sale,
                    ProductId = product.Id,
                    Qty = item.Qty,
                    UnitCost = product.CostPrice,
                    UnitSalePriceSnapshot = unitPriceArs,
                    UserId = ctx.User.UserId(),
                    Timestamp = DateTime.UtcNow
                });
            }

            sale.Subtotal = subtotal;
            sale.GlobalDiscount = Math.Max(0, request.GlobalDiscount ?? 0);
            if (sale.GlobalDiscount > subtotal) return Results.BadRequest(new { message = "Descuento total inválido" });
            sale.DiscountTotal = discount + sale.GlobalDiscount;
            sale.Total = subtotal - sale.DiscountTotal;

            db.Sales.Add(sale);
            await db.SaveChangesAsync();

            foreach (var move in pendingLedger)
            {
                move.ReferenceId = sale.Id;
                db.LedgerMovements.Add(move);
            }

            await db.SaveChangesAsync();
            await auditService.LogAsync(ctx.User.UserId(), "SALE_CREATE", "Sale", sale.Id.ToString(), sale.TicketNumber);
            await tx.CommitAsync();

            var saved = await db.Sales
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .Include(x => x.User)
                .AsNoTracking()
                .FirstAsync(x => x.Id == sale.Id);

            return Results.Ok(new
            {
                saved.Id,
                saved.TicketNumber,
                saved.Date,
                saved.Subtotal,
                saved.GlobalDiscount,
                saved.DiscountTotal,
                saved.Total,
                paymentMethod = saved.PaymentMethod.ToString(),
                status = saved.Status.ToString(),
                seller = saved.User?.Username,
                saved.CustomerId,
                items = saved.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    productName = i.Product?.Name,
                    i.Qty,
                    i.UnitPrice,
                    i.Discount,
                    lineTotal = (i.Qty * i.UnitPrice) - i.Discount
                })
            });
        }).AddEndpointFilter(async (context, next) =>
        {
            try
            {
                return await next(context);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = $"No se pudo finalizar la venta: {ex.Message}" });
            }
        });

        group.MapGet("/", async (DateTime? startDate, DateTime? endDate, AppDbContext db, HttpContext ctx) =>
        {
            var start = startDate?.Date ?? DateTime.Now.Date.AddDays(-30);
            var end = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Now.Date.AddDays(1).AddTicks(-1);

            var query = db.Sales
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .Include(x => x.User)
                .Where(x => x.Date >= start && x.Date <= end);

            if (!ctx.User.IsAdmin())
            {
                var uid = ctx.User.UserId();
                query = query.Where(x => x.UserId == uid);
            }

            var data = await query
                .AsNoTracking()
                .OrderByDescending(x => x.Date)
                .Take(1000)
                .Select(s => new
                {
                    s.Id,
                    s.TicketNumber,
                    s.Date,
                    s.PaymentMethod,
                    s.Status,
                    s.Subtotal,
                    s.DiscountTotal,
                    s.Total,
                    seller = s.User!.Username,
                    items = s.Items.Select(i => new
                    {
                        i.Id,
                        i.ProductId,
                        productName = i.Product!.Name,
                        i.Qty,
                        i.UnitPrice,
                        i.Discount
                    })
                })
                .ToListAsync();
            return Results.Ok(data);
        });

        group.MapGet("/{id:int}/receipt", async (int id, AppDbContext db, ReceiptService receiptService) =>
        {
            var sale = await db.Sales
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (sale is null) return Results.NotFound();

            var rowsHtml = string.Join("", sale.Items.Select(i =>
                $"<tr><td>{i.Product?.Name} x{i.Qty}</td><td style='text-align:right'>{(i.Qty * i.UnitPrice) - i.Discount:0.00}</td></tr>"));

            var html = $$"""
<!doctype html>
<html>
<head>
<meta charset=\"utf-8\" />
<title>Recibo {{sale.TicketNumber}}</title>
<style>
body { font-family: Arial, sans-serif; width: 80mm; margin: 0 auto; }
.ticket { padding: 8px; }
.line { border-top: 1px dashed #666; margin: 6px 0; }
table { width: 100%; font-size: 12px; }
@media print { button { display:none; } body { width: 80mm; } }
</style>
</head>
<body>
<div class=\"ticket\">
<h3 style=\"text-align:center;margin:0\">LB Electronica</h3>
<p>Ticket: {{sale.TicketNumber}}<br/>Fecha: {{sale.Date:yyyy-MM-dd HH:mm}}<br/>Vendedor: {{sale.User?.Username}}</p>
<div class=\"line\"></div>
<table>
{{rowsHtml}}
</table>
<div class=\"line\"></div>
<p>Subtotal: {{sale.Subtotal:0.00}}<br/>Desc. Items + Global: {{sale.DiscountTotal:0.00}}<br/><b>Total: {{sale.Total:0.00}}</b><br/>Pago: {{sale.PaymentMethod}}</p>
<p style=\"text-align:center\">Gracias por su compra</p>
<button onclick=\"window.print()\">Imprimir</button>
</div>
</body>
</html>
""";
            return Results.Content(html, "text/html");
        });

        group.MapGet("/{id:int}/whatsapp", async (int id, AppDbContext db, ReceiptService receiptService) =>
        {
            var sale = await db.Sales
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (sale is null) return Results.NotFound();
            var text = receiptService.BuildReceiptText(sale);
            var encoded = UrlEncoder.Default.Encode(text);
            return Results.Ok(new { url = $"https://wa.me/?text={encoded}" });
        });

        return group;
    }

    private static async Task<Customer?> UpsertCustomerAsync(CustomerUpsertRequest request, AppDbContext db)
    {
        var dni = request.Dni?.Trim();
        var name = request.Name?.Trim();
        var phone = request.Phone?.Trim();
        if (string.IsNullOrWhiteSpace(dni)) return null;

        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Dni == dni);
        if (customer is null)
        {
            customer = new Customer
            {
                Dni = dni,
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Customers.Add(customer);
            return customer;
        }

        customer.Active = true;
        if (!string.IsNullOrWhiteSpace(name)) customer.Name = name;
        if (!string.IsNullOrWhiteSpace(phone)) customer.Phone = phone;
        customer.UpdatedAt = DateTime.UtcNow;
        return customer;
    }
}
