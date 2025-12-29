using System.ServiceModel;

namespace Legacy.PricingRulesService;

[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IPricingRulesService
{
    [OperationContract]
    PricingRuleResponse[] GetAllRules();

    [OperationContract]
    PricingRuleResponse GetRule(int ruleId);

    [OperationContract]
    bool UpdateRule(UpdateRuleRequest request);
}

[MessageContract]
public class PricingRuleResponse
{
    [MessageBodyMember]
    public int Id { get; set; }

    [MessageBodyMember]
    public string Name { get; set; } = string.Empty;

    [MessageBodyMember]
    public string Description { get; set; } = string.Empty;

    [MessageBodyMember]
    public decimal Multiplier { get; set; }

    [MessageBodyMember]
    public bool IsActive { get; set; }
}

[MessageContract]
public class UpdateRuleRequest
{
    [MessageBodyMember]
    public int Id { get; set; }

    [MessageBodyMember]
    public bool IsActive { get; set; }
}

