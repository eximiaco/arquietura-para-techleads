using System.ServiceModel;

namespace Legacy.PricingRulesService;

[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IPricingRulesService
{
    [OperationContract]
    GetAllRulesResponse GetAllRules(GetAllRulesRequest request);

    [OperationContract]
    PricingRuleResponse GetRule(GetRuleRequest request);

    [OperationContract]
    UpdateRuleResponse UpdateRule(UpdateRuleRequest request);
}

[MessageContract]
public class GetAllRulesRequest
{
}

[MessageContract]
public class GetAllRulesResponse
{
    [MessageBodyMember]
    public PricingRuleResponse[] Rules { get; set; } = Array.Empty<PricingRuleResponse>();
}

[MessageContract]
public class GetRuleRequest
{
    [MessageBodyMember]
    public int RuleId { get; set; }
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

[MessageContract]
public class UpdateRuleResponse
{
    [MessageBodyMember]
    public bool Success { get; set; }
}

