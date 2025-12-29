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
    [MessageBodyMember]
    public string PolicyNumber { get; set; } = string.Empty;
}

[MessageContract]
public class GetPoliciesByCustomerRequest
{
    [MessageBodyMember]
    public int CustomerId { get; set; }
}

[MessageContract]
public class GetPoliciesByCustomerResponse
{
    [MessageBodyMember]
    public PolicyResponse[] Policies { get; set; } = Array.Empty<PolicyResponse>();
}

[MessageContract]
public class CreatePolicyRequest
{
    [MessageBodyMember]
    public int CustomerId { get; set; }

    [MessageBodyMember]
    public string VehiclePlate { get; set; } = string.Empty;

    [MessageBodyMember]
    public string VehicleModel { get; set; } = string.Empty;

    [MessageBodyMember]
    public int VehicleYear { get; set; }

    [MessageBodyMember]
    public decimal Premium { get; set; }
}

[MessageContract]
public class PolicyResponse
{
    [MessageBodyMember]
    public string PolicyNumber { get; set; } = string.Empty;

    [MessageBodyMember]
    public int CustomerId { get; set; }

    [MessageBodyMember]
    public string VehiclePlate { get; set; } = string.Empty;

    [MessageBodyMember]
    public decimal Premium { get; set; }

    [MessageBodyMember]
    public DateTime StartDate { get; set; }

    [MessageBodyMember]
    public DateTime EndDate { get; set; }

    [MessageBodyMember]
    public string Status { get; set; } = string.Empty;
}

