namespace LBElectronica.Server.Models;

public class LedgerMovement
{
    public int Id { get; set; }
    public LedgerMovementType MovementType { get; set; }
    public LedgerReferenceType ReferenceType { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int? ReferenceId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitSalePriceSnapshot { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
