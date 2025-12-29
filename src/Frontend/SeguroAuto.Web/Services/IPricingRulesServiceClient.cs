namespace SeguroAuto.Web.Services;

public interface IPricingRulesServiceClient
{
    Task<List<PricingRuleResponse>> GetAllRulesAsync();
    Task<PricingRuleResponse> GetRuleAsync(int ruleId);
    Task<bool> UpdateRuleAsync(int id, bool isActive);
}

public class PricingRuleResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Multiplier { get; set; }
    public bool IsActive { get; set; }
}

