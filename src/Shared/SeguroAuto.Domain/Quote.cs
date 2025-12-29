namespace SeguroAuto.Domain;

public class Quote
{
    public int Id { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string VehiclePlate { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public int VehicleYear { get; set; }
    public decimal Premium { get; set; }
    public QuoteStatus Status { get; set; }
    public DateTime ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum QuoteStatus
{
    Pending,
    Approved,
    Rejected,
    Expired,
    Converted
}

