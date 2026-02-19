namespace LBElectronica.Server.Models;

public class CashSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public decimal OpeningAmount { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? Difference { get; set; }
    public bool IsOpen { get; set; } = true;
    public ICollection<CashMovement> Movements { get; set; } = new List<CashMovement>();
}

public class CashMovement
{
    public int Id { get; set; }
    public int CashSessionId { get; set; }
    public CashSession? CashSession { get; set; }
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int UserId { get; set; }
    public User? User { get; set; }
}
