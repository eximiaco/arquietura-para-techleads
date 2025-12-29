namespace SeguroAuto.Domain;

public class Policy
{
    public int Id { get; set; }
    public string PolicyNumber { get; set; } = string.Empty; // Ex: "AUTO-1234"
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string VehiclePlate { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public int VehicleYear { get; set; }
    public decimal Premium { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public PolicyStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Claim> Claims { get; set; } = new();
}

public enum PolicyStatus
{
    Active,
    Expired,
    Cancelled,
    Suspended
}

