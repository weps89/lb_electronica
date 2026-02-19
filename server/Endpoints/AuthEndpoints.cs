using LBElectronica.Server.Data;
using LBElectronica.Server.DTOs;
using LBElectronica.Server.Models;
using LBElectronica.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (
            LoginRequest request,
            AppDbContext db,
            PasswordService passwordService,
            JwtService jwtService,
            AuditService auditService,
            HttpContext httpContext) =>
        {
            var normalizedUsername = request.Username.Trim();
            var user = await db.Users
                .FirstOrDefaultAsync(x => x.Username.ToLower() == normalizedUsername.ToLower());
            if (user is null || !user.IsActive || !passwordService.Verify(request.Password, user.PasswordHash))
                return Results.Unauthorized();

            var token = jwtService.Generate(user);
            httpContext.Response.Cookies.Append("lb_auth", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(12)
            });

            await auditService.LogAsync(user.Id, "LOGIN", "User", user.Id.ToString(), "User logged in");

            return Results.Ok(new
            {
                user.Id,
                user.Username,
                role = user.Role.ToString(),
                user.ForcePasswordChange
            });
        });

        group.MapPost("/logout", (HttpContext httpContext) =>
        {
            httpContext.Response.Cookies.Delete("lb_auth");
            return Results.Ok();
        });

        group.MapGet("/me", async (HttpContext httpContext, AppDbContext db) =>
        {
            var userId = httpContext.User.UserId();
            if (userId == 0) return Results.Unauthorized();

            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.Unauthorized();

            return Results.Ok(new { user.Id, user.Username, role = user.Role.ToString(), user.ForcePasswordChange });
        }).RequireAuthorization();

        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            HttpContext httpContext,
            AppDbContext db,
            PasswordService passwordService,
            AuditService auditService) =>
        {
            var userId = httpContext.User.UserId();
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.NotFound();

            if (!passwordService.Verify(request.CurrentPassword, user.PasswordHash))
                return Results.BadRequest(new { message = "La contraseña actual es inválida" });

            user.PasswordHash = passwordService.Hash(request.NewPassword);
            user.ForcePasswordChange = false;
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await auditService.LogAsync(userId, "PASSWORD_CHANGE", "User", userId.ToString(), "User changed password");
            return Results.Ok();
        }).RequireAuthorization();

        return group;
    }
}
