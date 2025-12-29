using System.ServiceModel;

namespace Legacy.PolicyService;

[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IPolicyService
{
    [OperationContract]
    PolicyResponse GetPolicy(string policyNumber);

    [OperationContract]
    PolicyResponse[] GetPoliciesByCustomer(int customerId);

    [OperationContract]
    PolicyResponse CreatePolicy(CreatePolicyRequest request);
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

