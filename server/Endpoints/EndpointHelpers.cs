using System.Security.Claims;
using LBElectronica.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace LBElectronica.Server.Endpoints;

public static class EndpointHelpers
{
    public static int UserId(this ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var parsed) ? parsed : 0;
    }

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(UserRole.Admin.ToString());

    public static IResult ValidationError(string message) => Results.BadRequest(new { message });

    public static DateTime ParseDateOrDefault(string? input, DateTime defaultValue)
    {
        return DateTime.TryParse(input, out var parsed) ? parsed : defaultValue;
    }
}
