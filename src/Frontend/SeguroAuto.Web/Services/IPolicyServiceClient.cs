namespace SeguroAuto.Web.Services;

public interface IPolicyServiceClient
{
    Task<PolicyResponse> GetPolicyAsync(string policyNumber);
    Task<List<PolicyResponse>> GetPoliciesByCustomerAsync(int customerId);
    Task<PolicyResponse> CreatePolicyAsync(int customerId, string vehiclePlate, string vehicleModel, int vehicleYear, decimal premium);
}

public class PolicyResponse
{
    public string PolicyNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string VehiclePlate { get; set; } = string.Empty;
    public decimal Premium { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

