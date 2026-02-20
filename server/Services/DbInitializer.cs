using LBElectronica.Server.Data;
using LBElectronica.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Services;

public class DbInitializer(
    AppDbContext db,
    PasswordService passwordService,
    ILogger<DbInitializer> logger)
{
    public async Task InitializeAsync()
    {
        await db.Database.EnsureCreatedAsync();
        var hasPendingSeedData = false;

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = passwordService.Hash("admin123!"),
                Role = UserRole.Admin,
                ForcePasswordChange = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            hasPendingSeedData = true;
            logger.LogInformation("Seeded default admin user");
        }

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product
                {
                    InternalCode = "P-000001",
                    Barcode = "7701001000011",
                    Name = "USB-C Cable 1m",
                    Category = "Accessories",
                    Brand = "Generic",
                    CostPrice = 3.50m,
                    MarginPercent = 80m,
                    SalePrice = 6.30m,
                    StockQuantity = 25,
                    StockMinimum = 5,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Product
                {
                    InternalCode = "P-000002",
                    Barcode = "7701001000028",
                    Name = "Wireless Mouse",
                    Category = "Peripherals",
                    Brand = "TechLine",
                    CostPrice = 8.00m,
                    MarginPercent = 70m,
                    SalePrice = 13.60m,
                    StockQuantity = 10,
                    StockMinimum = 3,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
            hasPendingSeedData = true;
            logger.LogInformation("Seeded sample products");
        }

        if (hasPendingSeedData)
            await db.SaveChangesAsync();

        if (!await db.ExchangeRates.AnyAsync())
        {
            var seedUserId = await db.Users
                .OrderBy(u => u.Id)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (seedUserId == 0)
            {
                logger.LogWarning("Skipped default exchange rate seed: no users available");
                return;
            }

            db.ExchangeRates.Add(new ExchangeRate
            {
                ArsPerUsd = 1450m,
                EffectiveDate = DateTime.UtcNow,
                UserId = seedUserId,
                CreatedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seeded default exchange rate");
            await db.SaveChangesAsync();
        }
    }
}
