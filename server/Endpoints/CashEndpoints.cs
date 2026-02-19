using LBElectronica.Server.Data;
using LBElectronica.Server.DTOs;
using LBElectronica.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Endpoints;

public static class CashEndpoints
{
    public static RouteGroupBuilder MapCash(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cash").RequireAuthorization();

        group.MapPost("/open", async (OpenCashSessionRequest request, AppDbContext db, HttpContext ctx) =>
        {
            var uid = ctx.User.UserId();
            var open = await db.CashSessions.FirstOrDefaultAsync(x => x.UserId == uid && x.IsOpen);
            if (open is not null) return Results.BadRequest(new { message = "Ya existe una sesión de caja abierta" });

            var session = new CashSession
            {
                UserId = uid,
                OpenedAt = DateTime.UtcNow,
                OpeningAmount = request.OpeningAmount,
                IsOpen = true
            };

            db.CashSessions.Add(session);
            await db.SaveChangesAsync();
            return Results.Ok(session);
        });

        group.MapGet("/current", async (AppDbContext db, HttpContext ctx) =>
        {
            var uid = ctx.User.UserId();
            var session = await db.CashSessions
                .Include(x => x.Movements)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == uid && x.IsOpen);
            if (session is null) return Results.Ok(null);
            return Results.Ok(new
            {
                session.Id,
                session.UserId,
                session.OpenedAt,
                session.OpeningAmount,
                session.ClosedAt,
                session.CountedCash,
                session.ExpectedCash,
                session.Difference,
                session.IsOpen,
                movements = session.Movements.Select(m => new
                {
                    m.Id,
                    m.Type,
                    m.Amount,
                    m.Reason,
                    m.Category,
                    m.CreatedAt
                })
            });
        });

        group.MapPost("/movement", async (CashMovementRequest request, AppDbContext db, HttpContext ctx) =>
        {
            var uid = ctx.User.UserId();
            var session = await db.CashSessions.FirstOrDefaultAsync(x => x.UserId == uid && x.IsOpen);
            if (session is null) return Results.BadRequest(new { message = "Primero debes abrir una sesión de caja" });

            if (request.Amount <= 0) return Results.BadRequest(new { message = "El monto debe ser mayor a 0" });

            db.CashMovements.Add(new CashMovement
            {
                CashSessionId = session.Id,
                Type = request.Type,
                Amount = request.Amount,
                Reason = request.Reason,
                Category = request.Category,
                CreatedAt = DateTime.UtcNow,
                UserId = uid
            });

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        group.MapGet("/pending-invoices", async (AppDbContext db) =>
        {
            var query = db.Sales
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Customer)
                .Include(x => x.Items)
                .Where(x => x.Status == SaleStatus.Pending);

            var data = await query
                .OrderBy(x => x.Date)
                .Take(500)
                .Select(x => new
                {
                    x.Id,
                    x.TicketNumber,
                    x.Date,
                    x.Total,
                    x.Subtotal,
                    x.DiscountTotal,
                    suggestedPaymentMethod = x.PaymentMethod.ToString(),
                    seller = x.User!.Username,
                    itemsCount = x.Items.Count,
                    customer = x.Customer == null
                        ? null
                        : new
                        {
                            x.Customer.Id,
                            x.Customer.Dni,
                            x.Customer.Name,
                            x.Customer.Phone
                        }
                })
                .ToListAsync();

            return Results.Ok(data);
        });

        group.MapGet("/customer-by-dni/{dni}", async (string dni, AppDbContext db) =>
        {
            var normalized = dni.Trim();
            if (string.IsNullOrWhiteSpace(normalized)) return Results.Ok(null);

            var customer = await db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Dni == normalized && x.Active);

            if (customer is null) return Results.Ok(null);
            return Results.Ok(new
            {
                customer.Id,
                customer.Dni,
                customer.Name,
                customer.Phone
            });
        });

        group.MapGet("/customer-search", async (string? q, AppDbContext db) =>
        {
            var term = q?.Trim();
            if (string.IsNullOrWhiteSpace(term)) return Results.Ok(Array.Empty<object>());

            var data = await db.Customers
                .AsNoTracking()
                .Where(x => x.Active &&
                            (x.Dni.Contains(term) ||
                             (x.Name != null && x.Name.Contains(term)) ||
                             (x.Phone != null && x.Phone.Contains(term))))
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Dni)
                .Take(20)
                .Select(x => new
                {
                    x.Id,
                    x.Dni,
                    x.Name,
                    x.Phone
                })
                .ToListAsync();

            return Results.Ok(data);
        });

        group.MapPost("/collect-invoice", async (CollectInvoiceRequest request, AppDbContext db, HttpContext ctx) =>
        {
            var uid = ctx.User.UserId();
            var session = await db.CashSessions.FirstOrDefaultAsync(x => x.UserId == uid && x.IsOpen);
            if (session is null) return Results.BadRequest(new { message = "Debes abrir caja para cobrar" });

            var sale = await db.Sales.FirstOrDefaultAsync(x => x.Id == request.SaleId);
            if (sale is null) return Results.NotFound();
            if (sale.Status != SaleStatus.Pending) return Results.BadRequest(new { message = "La factura no está pendiente" });

            if (request.Customer is not null)
            {
                var maybeCustomer = await UpsertCustomerAsync(request.Customer, db);
                if (maybeCustomer is not null)
                    sale.CustomerId = maybeCustomer.Id;
            }

            if (request.PaymentMethod == PaymentMethod.Cash)
            {
                if (!request.ReceivedAmount.HasValue) return Results.BadRequest(new { message = "Debes ingresar monto recibido" });
                if (request.ReceivedAmount.Value < sale.Total) return Results.BadRequest(new { message = "Monto recibido insuficiente" });
                sale.ReceivedAmount = request.ReceivedAmount.Value;
                sale.ChangeAmount = request.ReceivedAmount.Value - sale.Total;
                sale.OperationNumber = null;
                sale.Status = SaleStatus.Paid;
            }
            else if (request.PaymentMethod == PaymentMethod.Card)
            {
                if (string.IsNullOrWhiteSpace(request.OperationNumber)) return Results.BadRequest(new { message = "Debes ingresar número de operación" });
                sale.OperationNumber = request.OperationNumber.Trim();
                sale.ReceivedAmount = sale.Total;
                sale.ChangeAmount = 0;
                sale.Status = SaleStatus.Paid;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.OperationNumber)) return Results.BadRequest(new { message = "Debes ingresar número de operación" });
                sale.OperationNumber = request.OperationNumber.Trim();
                sale.ReceivedAmount = sale.Total;
                sale.ChangeAmount = 0;
                sale.Status = request.Verified ? SaleStatus.Verified : SaleStatus.Paid;
                sale.IsVerified = request.Verified;
            }

            sale.PaymentMethod = request.PaymentMethod;
            sale.PaidAt = DateTime.UtcNow;

            db.CashMovements.Add(new CashMovement
            {
                CashSessionId = session.Id,
                Type = CashMovementType.Income,
                Amount = sale.Total,
                Reason = $"VENTA {sale.TicketNumber}",
                Category = $"VENTA:{sale.PaymentMethod}",
                CreatedAt = DateTime.UtcNow,
                UserId = uid
            });

            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                sale.Id,
                sale.TicketNumber,
                status = sale.Status.ToString(),
                sale.PaymentMethod,
                sale.Total,
                sale.ReceivedAmount,
                sale.ChangeAmount,
                sale.OperationNumber,
                sale.CustomerId
            });
        });

        group.MapPost("/annul-invoice", async (AnnulInvoiceRequest request, AppDbContext db, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "Debes indicar motivo" });

            var sale = await db.Sales
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == request.SaleId);

            if (sale is null) return Results.NotFound();
            if (sale.Status != SaleStatus.Pending) return Results.BadRequest(new { message = "Solo puedes anular facturas pendientes" });

            await using var tx = await db.Database.BeginTransactionAsync();
            foreach (var item in sale.Items)
            {
                var product = await db.Products.FindAsync(item.ProductId);
                if (product is null) continue;
                product.StockQuantity += item.Qty;
                product.UpdatedAt = DateTime.UtcNow;

                db.LedgerMovements.Add(new LedgerMovement
                {
                    MovementType = LedgerMovementType.In,
                    ReferenceType = LedgerReferenceType.Sale,
                    ProductId = product.Id,
                    ReferenceId = sale.Id,
                    Qty = item.Qty,
                    UnitCost = item.CostPriceSnapshot,
                    UnitSalePriceSnapshot = item.UnitPrice,
                    UserId = ctx.User.UserId(),
                    Timestamp = DateTime.UtcNow
                });
            }

            sale.Status = SaleStatus.Cancelled;
            sale.CancelledAt = DateTime.UtcNow;
            sale.CancelledReason = request.Reason.Trim();
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok();
        });

        group.MapPost("/close", async (CloseCashSessionRequest request, AppDbContext db, HttpContext ctx) =>
        {
            var uid = ctx.User.UserId();
            var session = await db.CashSessions
                .Include(x => x.Movements)
                .FirstOrDefaultAsync(x => x.UserId == uid && x.IsOpen);
            if (session is null) return Results.BadRequest(new { message = "No se encontró una sesión abierta" });

            var incomes = session.Movements
                .Where(x => x.Type == CashMovementType.Income &&
                            (x.Category == null || !x.Category.StartsWith("VENTA:") || x.Category == "VENTA:Cash"))
                .Sum(x => x.Amount);
            var expenses = session.Movements.Where(x => x.Type == CashMovementType.Expense).Sum(x => x.Amount);

            var expected = session.OpeningAmount + incomes - expenses;
            session.ExpectedCash = expected;
            session.CountedCash = request.CountedCash;
            session.Difference = request.CountedCash - expected;
            session.ClosedAt = DateTime.UtcNow;
            session.IsOpen = false;

            await db.SaveChangesAsync();
            return Results.Ok(session);
        });

        group.MapGet("/my-day", async (DateTime? date, AppDbContext db, HttpContext ctx) =>
        {
            var target = (date ?? DateTime.Now).Date;
            var uid = ctx.User.UserId();
            var start = target;
            var end = target.AddDays(1).AddTicks(-1);

            var sales = await db.Sales
                .Where(x => x.UserId == uid && x.Date >= start && x.Date <= end && (x.Status == SaleStatus.Paid || x.Status == SaleStatus.Verified))
                .ToListAsync();
            var pendingCount = await db.Sales
                .Where(x => x.UserId == uid && x.Date >= start && x.Date <= end && x.Status == SaleStatus.Pending)
                .CountAsync();
            var sessions = await db.CashSessions.Include(x => x.Movements).Where(x => x.UserId == uid && x.OpenedAt >= start && x.OpenedAt <= end).ToListAsync();
            var cashMovements = sessions.SelectMany(x => x.Movements).ToList();

            var salesMovements = sales.Select(x => new
            {
                date = x.Date,
                type = "venta",
                reference = x.TicketNumber,
                amount = x.Total,
                paymentMethod = x.PaymentMethod.ToString()
            });

            var extraMovements = cashMovements.Select(x => new
            {
                date = x.CreatedAt,
                type = x.Type == CashMovementType.Income ? "ingreso" : "gasto",
                reference = x.Reason,
                amount = x.Amount,
                paymentMethod = "N/A"
            });

            var dayMovements = salesMovements
                .Concat(extraMovements)
                .OrderByDescending(x => x.date)
                .ToList();

            var result = new
            {
                salesCount = sales.Count,
                pendingInvoices = pendingCount,
                salesTotal = sales.Sum(x => x.Total),
                paymentBreakdown = sales.GroupBy(x => x.PaymentMethod.ToString()).ToDictionary(g => g.Key, g => g.Sum(x => x.Total)),
                incomes = cashMovements
                    .Where(x => x.Type == CashMovementType.Income &&
                                (x.Category == null || !x.Category.StartsWith("VENTA:") || x.Category == "VENTA:Cash"))
                    .Sum(x => x.Amount),
                expenses = cashMovements.Where(x => x.Type == CashMovementType.Expense).Sum(x => x.Amount),
                closureDiff = sessions.Where(x => !x.IsOpen).Sum(x => x.Difference ?? 0),
                dayMovements,
                sessions = sessions.Select(s => new
                {
                    s.Id,
                    s.OpenedAt,
                    s.OpeningAmount,
                    s.ClosedAt,
                    s.CountedCash,
                    s.ExpectedCash,
                    s.Difference,
                    s.IsOpen,
                    movements = s.Movements.Select(m => new
                    {
                        m.Id,
                        m.Type,
                        m.Amount,
                        m.Reason,
                        m.Category,
                        m.CreatedAt
                    })
                })
            };

            return Results.Ok(result);
        });

        group.MapGet("/sessions", async (DateTime? startDate, DateTime? endDate, AppDbContext db, HttpContext ctx) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();
            var start = startDate?.Date ?? DateTime.Now.Date.AddDays(-30);
            var end = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Now.Date.AddDays(1).AddTicks(-1);

            var sessions = await db.CashSessions
                .Include(x => x.User)
                .Include(x => x.Movements)
                .AsNoTracking()
                .Where(x => x.OpenedAt >= start && x.OpenedAt <= end)
                .OrderByDescending(x => x.OpenedAt)
                .ToListAsync();
            return Results.Ok(sessions.Select(s => new
            {
                s.Id,
                s.UserId,
                user = s.User == null ? null : new { s.User.Id, s.User.Username, role = s.User.Role.ToString() },
                s.OpenedAt,
                s.OpeningAmount,
                s.ClosedAt,
                s.CountedCash,
                s.ExpectedCash,
                s.Difference,
                s.IsOpen,
                movements = s.Movements.Select(m => new
                {
                    m.Id,
                    m.Type,
                    m.Amount,
                    m.Reason,
                    m.Category,
                    m.CreatedAt
                })
            }));
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
