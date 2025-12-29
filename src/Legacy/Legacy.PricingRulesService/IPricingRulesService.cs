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
    [MessageBodyMember(Order = 1)]
    public PricingRuleResponse[] Rules { get; set; } = Array.Empty<PricingRuleResponse>();
}

[MessageContract]
public class GetRuleRequest
{
    [MessageBodyMember(Order = 1)]
    public int RuleId { get; set; }
}

[MessageContract]
public class PricingRuleResponse
{
    [MessageBodyMember(Order = 1)]
    public int Id { get; set; }

    [MessageBodyMember(Order = 2)]
    public string Name { get; set; } = string.Empty;

    [MessageBodyMember(Order = 3)]
    public string Description { get; set; } = string.Empty;

    [MessageBodyMember(Order = 4)]
    public decimal Multiplier { get; set; }

    [MessageBodyMember(Order = 5)]
    public bool IsActive { get; set; }
}

[MessageContract]
public class UpdateRuleRequest
{
    [MessageBodyMember(Order = 1)]
    public int Id { get; set; }

    [MessageBodyMember(Order = 2)]
    public bool IsActive { get; set; }
}

[MessageContract]
public class UpdateRuleResponse
{
    [MessageBodyMember(Order = 1)]
    public bool Success { get; set; }
}

