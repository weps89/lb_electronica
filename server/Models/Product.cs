namespace LBElectronica.Server.Models;

public class Product
{
    public int Id { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? ImeiOrSerial { get; set; }
    public decimal CostPrice { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal SalePrice { get; set; }
    public decimal LastStockExchangeRateArs { get; set; }
    public decimal StockQuantity { get; set; }
    public int StockMinimum { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
