namespace SeguroAuto.Domain;

public class PricingRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Multiplier { get; set; } // Multiplicador de prêmio (ex: 1.2 = +20%)
    public string Condition { get; set; } = string.Empty; // Condição de aplicação (ex: "VehicleYear < 2010")
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

