using System.ServiceModel;

namespace Legacy.ClaimsService;

[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IClaimsService
{
    [OperationContract]
    ClaimResponse GetClaim(GetClaimRequest request);

    [OperationContract]
    GetClaimsByPolicyResponse GetClaimsByPolicy(GetClaimsByPolicyRequest request);

    [OperationContract]
    ClaimResponse CreateClaim(CreateClaimRequest request);
}

[MessageContract]
public class GetClaimRequest
{
    [MessageBodyMember(Order = 1)]
    public string ClaimNumber { get; set; } = string.Empty;
}

[MessageContract]
public class GetClaimsByPolicyRequest
{
    [MessageBodyMember(Order = 1)]
    public string PolicyNumber { get; set; } = string.Empty;
}

[MessageContract]
public class GetClaimsByPolicyResponse
{
    [MessageBodyMember(Order = 1)]
    public ClaimResponse[] Claims { get; set; } = Array.Empty<ClaimResponse>();
}

[MessageContract]
public class CreateClaimRequest
{
    [MessageBodyMember(Order = 1)]
    public string PolicyNumber { get; set; } = string.Empty;

    [MessageBodyMember(Order = 2)]
    public string Description { get; set; } = string.Empty;

    [MessageBodyMember(Order = 3)]
    public decimal Amount { get; set; }

    [MessageBodyMember(Order = 4)]
    public DateTime IncidentDate { get; set; }
}

[MessageContract]
public class ClaimResponse
{
    [MessageBodyMember(Order = 1)]
    public string ClaimNumber { get; set; } = string.Empty;

    [MessageBodyMember(Order = 2)]
    public string PolicyNumber { get; set; } = string.Empty;

    [MessageBodyMember(Order = 3)]
    public string Description { get; set; } = string.Empty;

    [MessageBodyMember(Order = 4)]
    public decimal Amount { get; set; }

    [MessageBodyMember(Order = 5)]
    public string Status { get; set; } = string.Empty;

    [MessageBodyMember(Order = 6)]
    public DateTime IncidentDate { get; set; }
}

