using LBElectronica.Server.Data;
using LBElectronica.Server.Models;

namespace LBElectronica.Server.Services;

public class AuditService(AppDbContext db)
{
    public async Task LogAsync(int? userId, string action, string? entityName = null, string? entityId = null, string? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
