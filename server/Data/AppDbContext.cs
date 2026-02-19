using LBElectronica.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace LBElectronica.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockEntry> StockEntries => Set<StockEntry>();
    public DbSet<StockEntryItem> StockEntryItems => Set<StockEntryItem>();
    public DbSet<LedgerMovement> LedgerMovements => Set<LedgerMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.InternalCode).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Barcode).IsUnique();
        modelBuilder.Entity<Sale>().HasIndex(x => x.TicketNumber).IsUnique();
        modelBuilder.Entity<StockEntry>().HasIndex(x => x.BatchCode).IsUnique();
        modelBuilder.Entity<Customer>().HasIndex(x => x.Dni).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(x => x.Name);
        modelBuilder.Entity<ProductCategory>().HasIndex(x => x.Name).IsUnique();

        modelBuilder.Entity<Product>()
            .Property(x => x.CostPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Product>()
            .Property(x => x.MarginPercent)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Product>()
            .Property(x => x.SalePrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Product>()
            .Property(x => x.LastStockExchangeRateArs)
            .HasPrecision(18, 4);

        modelBuilder.Entity<Product>()
            .Property(x => x.StockQuantity)
            .HasPrecision(18, 2);

        modelBuilder.Entity<LedgerMovement>()
            .Property(x => x.Qty)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SaleItem>()
            .Property(x => x.Qty)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SaleItem>()
            .Property(x => x.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SaleItem>()
            .Property(x => x.Discount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SaleItem>()
            .Property(x => x.CostPriceSnapshotArs)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ExchangeRate>()
            .Property(x => x.ArsPerUsd)
            .HasPrecision(18, 4);

        modelBuilder.Entity<StockEntry>()
            .Property(x => x.LogisticsUsd)
            .HasPrecision(18, 4);

        modelBuilder.Entity<StockEntry>()
            .Property(x => x.ExchangeRateArs)
            .HasPrecision(18, 4);

        modelBuilder.Entity<StockEntryItem>()
            .Property(x => x.PurchaseUnitCostUsd)
            .HasPrecision(18, 4);

        modelBuilder.Entity<StockEntryItem>()
            .Property(x => x.LogisticsUnitCostUsd)
            .HasPrecision(18, 6);

        modelBuilder.Entity<StockEntryItem>()
            .Property(x => x.FinalUnitCostUsd)
            .HasPrecision(18, 6);

        modelBuilder.Entity<StockEntryItem>()
            .Property(x => x.FinalUnitCostArs)
            .HasPrecision(18, 2);
    }
}
