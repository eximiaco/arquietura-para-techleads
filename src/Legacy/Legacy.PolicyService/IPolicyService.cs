using System.ServiceModel;

namespace Legacy.PolicyService;

[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IPolicyService
{
    [OperationContract]
    PolicyResponse GetPolicy(GetPolicyRequest request);

    [OperationContract]
    GetPoliciesByCustomerResponse GetPoliciesByCustomer(GetPoliciesByCustomerRequest request);

    [OperationContract]
    PolicyResponse CreatePolicy(CreatePolicyRequest request);
}

[MessageContract]
public class GetPolicyRequest
{
    [MessageBodyMember(Order = 1)]
    public string PolicyNumber { get; set; } = string.Empty;
}

[MessageContract]
public class GetPoliciesByCustomerRequest
{
    [MessageBodyMember(Order = 1)]
    public int CustomerId { get; set; }
}

[MessageContract]
public class GetPoliciesByCustomerResponse
{
    [MessageBodyMember(Order = 1)]
    public PolicyResponse[] Policies { get; set; } = Array.Empty<PolicyResponse>();
}

[MessageContract]
public class CreatePolicyRequest
{
    [MessageBodyMember(Order = 1)]
    public int CustomerId { get; set; }

    [MessageBodyMember(Order = 2)]
    public string VehiclePlate { get; set; } = string.Empty;

    [MessageBodyMember(Order = 3)]
    public string VehicleModel { get; set; } = string.Empty;

    [MessageBodyMember(Order = 4)]
    public int VehicleYear { get; set; }

    [MessageBodyMember(Order = 5)]
    public decimal Premium { get; set; }
}

[MessageContract]
public class PolicyResponse
{
    [MessageBodyMember(Order = 1)]
    public string PolicyNumber { get; set; } = string.Empty;

    [MessageBodyMember(Order = 2)]
    public int CustomerId { get; set; }

    [MessageBodyMember(Order = 3)]
    public string VehiclePlate { get; set; } = string.Empty;

    [MessageBodyMember(Order = 4)]
    public decimal Premium { get; set; }

    [MessageBodyMember(Order = 5)]
    public DateTime StartDate { get; set; }

    [MessageBodyMember(Order = 6)]
    public DateTime EndDate { get; set; }

    [MessageBodyMember(Order = 7)]
    public string Status { get; set; } = string.Empty;
}

