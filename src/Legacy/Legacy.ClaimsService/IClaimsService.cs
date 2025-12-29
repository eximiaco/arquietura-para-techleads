using System.ServiceModel;

namespace Legacy.ClaimsService;

[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IClaimsService
{
    [OperationContract]
    ClaimResponse GetClaim(string claimNumber);

    [OperationContract]
    ClaimResponse[] GetClaimsByPolicy(string policyNumber);

    [OperationContract]
    ClaimResponse CreateClaim(CreateClaimRequest request);
}

[MessageContract]
public class CreateClaimRequest
{
    [MessageBodyMember]
    public string PolicyNumber { get; set; } = string.Empty;

    [MessageBodyMember]
    public string Description { get; set; } = string.Empty;

    [MessageBodyMember]
    public decimal Amount { get; set; }

    [MessageBodyMember]
    public DateTime IncidentDate { get; set; }
}

[MessageContract]
public class ClaimResponse
{
    [MessageBodyMember]
    public string ClaimNumber { get; set; } = string.Empty;

    [MessageBodyMember]
    public string PolicyNumber { get; set; } = string.Empty;

    [MessageBodyMember]
    public string Description { get; set; } = string.Empty;

    [MessageBodyMember]
    public decimal Amount { get; set; }

    [MessageBodyMember]
    public string Status { get; set; } = string.Empty;

    [MessageBodyMember]
    public DateTime IncidentDate { get; set; }
}

