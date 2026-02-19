using LBElectronica.Server.Data;
using LBElectronica.Server.DTOs;
using LBElectronica.Server.Models;
using LBElectronica.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUsers(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

        group.MapGet("/", async (AppDbContext db) =>
        {
            var users = await db.Users
                .OrderBy(x => x.Username)
                .Select(x => new
                {
                    x.Id,
                    x.Username,
                    role = x.Role.ToString(),
                    x.IsActive,
                    x.ForcePasswordChange,
                    x.CreatedAt
                })
                .ToListAsync();
            return Results.Ok(users);
        });

        group.MapPost("/", async (CreateUserRequest request, AppDbContext db, PasswordService passwordService, AuditService auditService, HttpContext ctx) =>
        {
            var normalizedUsername = request.Username.Trim();
            if (await db.Users.AnyAsync(x => x.Username.ToLower() == normalizedUsername.ToLower()))
                return Results.BadRequest(new { message = "El usuario ya existe" });

            var user = new User
            {
                Username = normalizedUsername,
                PasswordHash = passwordService.Hash(request.Password),
                Role = request.Role,
                IsActive = true,
                ForcePasswordChange = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
            await auditService.LogAsync(ctx.User.UserId(), "USER_CREATE", "User", user.Id.ToString(), $"Created user {user.Username}");
            return Results.Ok();
        });

        group.MapPost("/reset-password", async (ResetPasswordRequest request, AppDbContext db, PasswordService passwordService, AuditService auditService, HttpContext ctx) =>
        {
            var user = await db.Users.FindAsync(request.UserId);
            if (user is null) return Results.NotFound();

            user.PasswordHash = passwordService.Hash(request.NewPassword);
            user.ForcePasswordChange = true;
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await auditService.LogAsync(ctx.User.UserId(), "USER_RESET_PASSWORD", "User", user.Id.ToString(), $"Reset password for {user.Username}");

            return Results.Ok();
        });

        group.MapPut("/{id:int}", async (int id, UpdateUserRequest request, AppDbContext db, PasswordService passwordService, AuditService auditService, HttpContext ctx) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            var oldRole = user.Role;
            user.Role = request.Role;

            var changedPassword = !string.IsNullOrWhiteSpace(request.NewPassword);
            if (changedPassword)
            {
                user.PasswordHash = passwordService.Hash(request.NewPassword!);
                user.ForcePasswordChange = true;
            }

            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await auditService.LogAsync(
                ctx.User.UserId(),
                "USER_UPDATE",
                "User",
                user.Id.ToString(),
                $"Role {oldRole}=>{user.Role}; PasswordChanged: {changedPassword}");

            return Results.Ok(new
            {
                user.Id,
                user.Username,
                role = user.Role.ToString(),
                user.IsActive,
                user.ForcePasswordChange
            });
        });

        group.MapPatch("/{id:int}/active", async (int id, bool active, AppDbContext db, AuditService auditService, HttpContext ctx) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();
            user.IsActive = active;
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await auditService.LogAsync(ctx.User.UserId(), "USER_STATUS_CHANGE", "User", user.Id.ToString(), $"Active: {active}");
            return Results.Ok();
        });

        return group;
    }
}
