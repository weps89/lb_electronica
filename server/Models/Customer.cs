namespace LBElectronica.Server.Models;

public class Customer
{
    public int Id { get; set; }
    public string Dni { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
