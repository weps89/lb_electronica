using LBElectronica.Server.Data;
using LBElectronica.Server.DTOs;
using LBElectronica.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Endpoints;

public static class ConfigEndpoints
{
    public static RouteGroupBuilder MapConfig(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/config").RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

        group.MapGet("/customers", async (string? q, AppDbContext db) =>
        {
            var term = q?.Trim().ToLower();
            var query = db.Customers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(term))
            {
                query = query.Where(x =>
                    x.Dni.ToLower().Contains(term) ||
                    (x.Name != null && x.Name.ToLower().Contains(term)) ||
                    (x.Phone != null && x.Phone.ToLower().Contains(term)));
            }

            var data = await query.OrderBy(x => x.Name).ThenBy(x => x.Dni).Take(500).ToListAsync();
            return Results.Ok(data);
        });

        group.MapPost("/customers", async (CustomerAdminUpsertRequest request, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Dni)) return Results.BadRequest(new { message = "DNI requerido" });
            var dni = request.Dni.Trim();
            if (await db.Customers.AnyAsync(x => x.Dni == dni))
                return Results.BadRequest(new { message = "Ya existe un cliente con ese DNI" });

            var item = new Customer
            {
                Dni = dni,
                Name = request.Name?.Trim(),
                Phone = request.Phone?.Trim(),
                Active = request.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Customers.Add(item);
            await db.SaveChangesAsync();
            return Results.Ok(item);
        });

        group.MapPut("/customers/{id:int}", async (int id, CustomerAdminUpsertRequest request, AppDbContext db) =>
        {
            var item = await db.Customers.FindAsync(id);
            if (item is null) return Results.NotFound();
            var dni = request.Dni.Trim();
            if (await db.Customers.AnyAsync(x => x.Id != id && x.Dni == dni))
                return Results.BadRequest(new { message = "Ya existe un cliente con ese DNI" });

            item.Dni = dni;
            item.Name = request.Name?.Trim();
            item.Phone = request.Phone?.Trim();
            item.Active = request.Active;
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(item);
        });

        group.MapGet("/suppliers", async (string? q, AppDbContext db) =>
        {
            var term = q?.Trim().ToLower();
            var query = db.Suppliers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(term))
            {
                query = query.Where(x =>
                    x.Name.ToLower().Contains(term) ||
                    (x.TaxId != null && x.TaxId.ToLower().Contains(term)) ||
                    (x.Phone != null && x.Phone.ToLower().Contains(term)));
            }

            var data = await query.OrderBy(x => x.Name).Take(500).ToListAsync();
            return Results.Ok(data);
        });

        group.MapPost("/suppliers", async (SupplierUpsertRequest request, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Nombre requerido" });
            var item = new Supplier
            {
                Name = request.Name.Trim(),
                TaxId = request.TaxId?.Trim(),
                Phone = request.Phone?.Trim(),
                Address = request.Address?.Trim(),
                Active = request.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Suppliers.Add(item);
            await db.SaveChangesAsync();
            return Results.Ok(item);
        });

        group.MapPut("/suppliers/{id:int}", async (int id, SupplierUpsertRequest request, AppDbContext db) =>
        {
            var item = await db.Suppliers.FindAsync(id);
            if (item is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Nombre requerido" });

            item.Name = request.Name.Trim();
            item.TaxId = request.TaxId?.Trim();
            item.Phone = request.Phone?.Trim();
            item.Address = request.Address?.Trim();
            item.Active = request.Active;
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(item);
        });

        group.MapGet("/categories", async (string? q, AppDbContext db) =>
        {
            var term = q?.Trim().ToLower();
            var query = db.ProductCategories.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(term))
                query = query.Where(x => x.Name.ToLower().Contains(term));
            var data = await query.OrderBy(x => x.Name).Take(500).ToListAsync();
            return Results.Ok(data);
        });

        group.MapPost("/categories", async (ProductCategoryUpsertRequest request, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Nombre requerido" });
            var name = request.Name.Trim();
            if (await db.ProductCategories.AnyAsync(x => x.Name == name))
                return Results.BadRequest(new { message = "La categoría ya existe" });
            var item = new ProductCategory
            {
                Name = name,
                Active = request.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ProductCategories.Add(item);
            await db.SaveChangesAsync();
            return Results.Ok(item);
        });

        group.MapPut("/categories/{id:int}", async (int id, ProductCategoryUpsertRequest request, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Nombre requerido" });
            var item = await db.ProductCategories.FindAsync(id);
            if (item is null) return Results.NotFound();
            var oldName = item.Name;
            var newName = request.Name.Trim();
            if (await db.ProductCategories.AnyAsync(x => x.Id != id && x.Name == newName))
                return Results.BadRequest(new { message = "La categoría ya existe" });

            item.Name = newName;
            item.Active = request.Active;
            item.UpdatedAt = DateTime.UtcNow;

            if (!string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                var products = await db.Products.Where(x => x.Category == oldName).ToListAsync();
                foreach (var p in products)
                {
                    p.Category = newName;
                    p.UpdatedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(item);
        });

        return group;
    }
}
