namespace LBElectronica.Server.Models;

public class StockEntry
{
    public int Id { get; set; }
    public string? BatchCode { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Supplier { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Notes { get; set; }
    public decimal LogisticsUsd { get; set; }
    public decimal ExchangeRateArs { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<StockEntryItem> Items { get; set; } = new List<StockEntryItem>();
}

public class StockEntryItem
{
    public int Id { get; set; }
    public int StockEntryId { get; set; }
    public StockEntry? StockEntry { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Qty { get; set; }
    public decimal CostPrice { get; set; }
    public decimal PurchaseUnitCostUsd { get; set; }
    public decimal LogisticsUnitCostUsd { get; set; }
    public decimal FinalUnitCostUsd { get; set; }
    public decimal FinalUnitCostArs { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal SalePriceSnapshot { get; set; }
}
