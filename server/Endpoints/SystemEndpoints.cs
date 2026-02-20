using LBElectronica.Server.Data;
using LBElectronica.Server.DTOs;
using LBElectronica.Server.Models;
using LBElectronica.Server.Services;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace LBElectronica.Server.Endpoints;

public static class SystemEndpoints
{
    private record BackupCloudConfig(
        string Provider,
        string RemoteName,
        string RemoteFolder,
        int KeepLocalDays,
        int KeepRemoteDays,
        string ScheduleAt,
        bool Enabled,
        DateTime UpdatedAt,
        int UpdatedByUserId
    );

    private static string BackupConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "config", "backup-cloud.json");

    public static RouteGroupBuilder MapSystem(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system").RequireAuthorization();

        group.MapGet("/dashboard", async (AppDbContext db, HttpContext ctx) =>
        {
            var todayStart = DateTime.Now.Date;
            var todayEnd = todayStart.AddDays(1).AddTicks(-1);

            if (ctx.User.IsAdmin())
            {
                var paidSales = await db.Sales
                    .Where(x => x.Date >= todayStart && x.Date <= todayEnd && x.Status != SaleStatus.Cancelled)
                    .Select(x => new { x.Id, x.Total })
                    .ToListAsync();
                var todaySales = paidSales.Sum(x => x.Total);
                var expenses = (await db.CashMovements
                    .Where(x => x.CreatedAt >= todayStart && x.CreatedAt <= todayEnd && x.Type == CashMovementType.Expense)
                    .Select(x => x.Amount)
                    .ToListAsync()).Sum();
                var saleItems = await db.SaleItems
                    .Where(x => paidSales.Select(s => s.Id).Contains(x.SaleId))
                    .Select(x => new { x.SaleId, x.Qty, x.CostPriceSnapshotArs, x.CostPriceSnapshot })
                    .ToListAsync();
                var costMap = saleItems
                    .GroupBy(x => x.SaleId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => (x.CostPriceSnapshotArs > 0 ? x.CostPriceSnapshotArs : x.CostPriceSnapshot) * x.Qty));
                var gross = paidSales.Sum(s => s.Total - costMap.GetValueOrDefault(s.Id, 0m));

                return Results.Ok(new
                {
                    role = "Admin",
                    todaySales,
                    grossProfit = gross,
                    expenses,
                    netProfit = gross - expenses,
                    lowStockCount = await db.Products.CountAsync(x => x.StockQuantity <= x.StockMinimum)
                });
            }

            var uid = ctx.User.UserId();
            var mySales = await db.Sales.Where(x => x.UserId == uid && x.Date >= todayStart && x.Date <= todayEnd).SumAsync(x => (decimal?)x.Total) ?? 0;
            var openSession = await db.CashSessions.Include(x => x.Movements).FirstOrDefaultAsync(x => x.UserId == uid && x.IsOpen);
            var expected = 0m;
            if (openSession is not null)
            {
                var incomes = openSession.Movements
                    .Where(x => x.Type == CashMovementType.Income &&
                                (x.Category == null || !x.Category.StartsWith("VENTA:") || x.Category == "VENTA:Cash"))
                    .Sum(x => x.Amount);
                var expenses = openSession.Movements.Where(x => x.Type == CashMovementType.Expense).Sum(x => x.Amount);
                expected = openSession.OpeningAmount + incomes - expenses;
            }

            return Results.Ok(new
            {
                role = "Cashier",
                todaySales = mySales,
                hasOpenCashSession = openSession is not null,
                expectedCash = expected
            });
        });

        group.MapPost("/backup", async (IConfiguration config, HttpContext ctx) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();

            var dbPath = config.GetConnectionString("Default")?.Replace("Data Source=", "").Trim() ?? "lb_electronica.db";
            if (!Path.IsPathRooted(dbPath)) dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);

            if (!File.Exists(dbPath)) return Results.BadRequest(new { message = "No se encontró el archivo de base de datos" });

            var backupDir = Path.Combine(AppContext.BaseDirectory, "backups");
            Directory.CreateDirectory(backupDir);
            var backupFile = Path.Combine(backupDir, $"lb_electronica_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            File.Copy(dbPath, backupFile, true);

            return Results.Ok(new { file = backupFile });
        });

        group.MapGet("/exchange-rate", async (AppDbContext db) =>
        {
            var rate = await db.ExchangeRates
                .AsNoTracking()
                .OrderByDescending(x => x.EffectiveDate)
                .Select(x => new { x.Id, x.ArsPerUsd, x.EffectiveDate, x.CreatedAt, x.UserId })
                .FirstOrDefaultAsync();
            if (rate is null) return Results.Ok(new { ArsPerUsd = 1m });
            return Results.Ok(rate);
        });

        group.MapPost("/exchange-rate", async (ExchangeRateRequest request, AppDbContext db, HttpContext ctx, AuditService auditService) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();
            if (request.ArsPerUsd <= 0) return Results.BadRequest(new { message = "Cotización inválida" });

            var rate = new ExchangeRate
            {
                ArsPerUsd = request.ArsPerUsd,
                EffectiveDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UserId = ctx.User.UserId()
            };
            db.ExchangeRates.Add(rate);
            await db.SaveChangesAsync();
            await auditService.LogAsync(ctx.User.UserId(), "EXCHANGE_RATE_SET", "ExchangeRate", rate.Id.ToString(), request.ArsPerUsd.ToString());
            return Results.Ok(rate);
        });

        group.MapGet("/backup-cloud-config", async (HttpContext ctx) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();

            if (!File.Exists(BackupConfigPath))
            {
                return Results.Ok(new BackupCloudConfig(
                    Provider: "Google Drive",
                    RemoteName: "gdrive",
                    RemoteFolder: "LBElectronica/backups",
                    KeepLocalDays: 30,
                    KeepRemoteDays: 90,
                    ScheduleAt: "22:00",
                    Enabled: false,
                    UpdatedAt: DateTime.UtcNow,
                    UpdatedByUserId: 0
                ));
            }

            var json = await File.ReadAllTextAsync(BackupConfigPath);
            var cfg = JsonSerializer.Deserialize<BackupCloudConfig>(json);
            return Results.Ok(cfg);
        });

        group.MapPost("/backup-cloud-config", async (BackupCloudConfigRequest request, HttpContext ctx, AuditService auditService) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.RemoteName)) return Results.BadRequest(new { message = "Remote name requerido" });
            if (string.IsNullOrWhiteSpace(request.RemoteFolder)) return Results.BadRequest(new { message = "Remote folder requerido" });
            if (request.KeepLocalDays < 1 || request.KeepRemoteDays < 1) return Results.BadRequest(new { message = "Retención inválida" });

            var cfg = new BackupCloudConfig(
                Provider: string.IsNullOrWhiteSpace(request.Provider) ? "Google Drive" : request.Provider.Trim(),
                RemoteName: request.RemoteName.Trim(),
                RemoteFolder: request.RemoteFolder.Trim(),
                KeepLocalDays: request.KeepLocalDays,
                KeepRemoteDays: request.KeepRemoteDays,
                ScheduleAt: request.ScheduleAt?.Trim() ?? "22:00",
                Enabled: request.Enabled,
                UpdatedAt: DateTime.UtcNow,
                UpdatedByUserId: ctx.User.UserId()
            );

            var dir = Path.GetDirectoryName(BackupConfigPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(BackupConfigPath, json);
            await auditService.LogAsync(ctx.User.UserId(), "BACKUP_CONFIG_UPDATE", "System", "backup-cloud", $"{cfg.RemoteName}:{cfg.RemoteFolder}");
            return Results.Ok(cfg);
        });

        group.MapPost("/backup-cloud-config/test", async (HttpContext ctx) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();
            if (!File.Exists(BackupConfigPath)) return Results.BadRequest(new { message = "Configura backup primero" });

            var cfg = JsonSerializer.Deserialize<BackupCloudConfig>(await File.ReadAllTextAsync(BackupConfigPath));
            if (cfg is null) return Results.BadRequest(new { message = "Config inválida" });

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "rclone",
                    Arguments = $"lsd {cfg.RemoteName}:{cfg.RemoteFolder}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc is null) return Results.BadRequest(new { message = "No se pudo iniciar rclone" });
                await proc.WaitForExitAsync();
                var err = await proc.StandardError.ReadToEndAsync();
                if (proc.ExitCode != 0) return Results.BadRequest(new { message = string.IsNullOrWhiteSpace(err) ? "Conexión fallida" : err.Trim() });
                return Results.Ok(new { ok = true, message = "Conexión correcta con backup cloud" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPost("/backup-cloud-run-now", async (HttpContext ctx, AuditService auditService) =>
        {
            if (!ctx.User.IsAdmin()) return Results.Forbid();
            if (!File.Exists(BackupConfigPath)) return Results.BadRequest(new { message = "Configura backup primero" });

            var cfg = JsonSerializer.Deserialize<BackupCloudConfig>(await File.ReadAllTextAsync(BackupConfigPath));
            if (cfg is null) return Results.BadRequest(new { message = "Config inválida" });

            var scriptCandidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "..", "scripts", "backup-cloud.ps1"),
                Path.Combine(Directory.GetCurrentDirectory(), "scripts", "backup-cloud.ps1"),
                Path.Combine(AppContext.BaseDirectory, "..", "scripts", "backup-cloud.ps1"),
                Path.Combine(AppContext.BaseDirectory, "scripts", "backup-cloud.ps1")
            };
            var scriptPath = scriptCandidates.FirstOrDefault(File.Exists);
            if (scriptPath is null) return Results.BadRequest(new { message = "No se encontró script backup-cloud.ps1" });

            var shell = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell.exe" : "pwsh";
            var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -RemoteName \"{cfg.RemoteName}\" -RemoteFolder \"{cfg.RemoteFolder}\" -KeepLocalDays {cfg.KeepLocalDays} -KeepRemoteDays {cfg.KeepRemoteDays}";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc is null) return Results.BadRequest(new { message = "No se pudo iniciar proceso de backup" });

                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                await proc.WaitForExitAsync(cts.Token);
                var stdOut = await proc.StandardOutput.ReadToEndAsync();
                var stdErr = await proc.StandardError.ReadToEndAsync();

                if (proc.ExitCode != 0)
                {
                    return Results.BadRequest(new
                    {
                        message = string.IsNullOrWhiteSpace(stdErr) ? "Backup falló" : stdErr.Trim()
                    });
                }

                await auditService.LogAsync(ctx.User.UserId(), "BACKUP_RUN_NOW", "System", "backup-cloud", $"{cfg.RemoteName}:{cfg.RemoteFolder}");
                return Results.Ok(new { ok = true, message = "Respaldo ejecutado correctamente.", output = stdOut.Trim() });
            }
            catch (OperationCanceledException)
            {
                return Results.BadRequest(new { message = "Timeout: el backup demoró demasiado." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return group;
    }
}
