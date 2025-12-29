namespace SeguroAuto.Web.Services;

public interface IClaimsServiceClient
{
    Task<ClaimResponse> GetClaimAsync(string claimNumber);
    Task<List<ClaimResponse>> GetClaimsByPolicyAsync(string policyNumber);
    Task<ClaimResponse> CreateClaimAsync(string policyNumber, string description, decimal amount, DateTime incidentDate);
}

public class ClaimResponse
{
    public string ClaimNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime IncidentDate { get; set; }
}

