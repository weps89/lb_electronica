namespace LBElectronica.Server.Models;

public class ExchangeRate
{
    public int Id { get; set; }
    public decimal ArsPerUsd { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
