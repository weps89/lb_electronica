namespace LBElectronica.Server.Models;

public class Sale
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public int UserId { get; set; }
    public User? User { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Pending;
    public DateTime? PaidAt { get; set; }
    public decimal? ReceivedAmount { get; set; }
    public decimal? ChangeAmount { get; set; }
    public string? OperationNumber { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledReason { get; set; }
    public decimal Subtotal { get; set; }
    public decimal GlobalDiscount { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal CostPriceSnapshot { get; set; }
    public decimal SalePriceSnapshot { get; set; }
    public string? ImeiOrSerial { get; set; }
}
