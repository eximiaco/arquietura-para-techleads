namespace SeguroAuto.Web.Models;

public class PricingRuleViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Multiplier { get; set; }
    public bool IsActive { get; set; }
}

