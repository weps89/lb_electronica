using LBElectronica.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Services;

public class CodeService(AppDbContext db)
{
    public async Task<string> NextProductCodeAsync()
    {
        var lastCode = await db.Products
            .OrderByDescending(x => x.Id)
            .Select(x => x.InternalCode)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrWhiteSpace(lastCode) && lastCode.StartsWith("P-"))
        {
            var numPart = lastCode[2..];
            if (int.TryParse(numPart, out var parsed))
                next = parsed + 1;
        }

        return $"P-{next:D6}";
    }

    public async Task<string> NextTicketNumberAsync()
    {
        var today = DateTime.Now;
        var prefix = $"T-{today:yyyyMMdd}-";
        var count = await db.Sales.CountAsync(x => x.TicketNumber.StartsWith(prefix));
        return $"{prefix}{count + 1:D4}";
    }
}
