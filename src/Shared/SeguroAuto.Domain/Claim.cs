namespace SeguroAuto.Domain;

public class Claim
{
    public int Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public int PolicyId { get; set; }
    public Policy Policy { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public ClaimStatus Status { get; set; }
    public DateTime IncidentDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum ClaimStatus
{
    Pending,
    UnderReview,
    Approved,
    Rejected,
    Paid
}

