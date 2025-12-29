using System.ServiceModel;

namespace Legacy.QuoteService;

[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IQuoteService
{
    [OperationContract]
    QuoteResponse GetQuote(QuoteRequest request);

    [OperationContract]
    GetQuotesByCustomerResponse GetQuotesByCustomer(GetQuotesByCustomerRequest request);

    [OperationContract]
    ApproveQuoteResponse ApproveQuote(ApproveQuoteRequest request);
}

[MessageContract]
public class QuoteRequest
{
    [MessageBodyMember]
    public int CustomerId { get; set; }

    [MessageBodyMember]
    public string VehiclePlate { get; set; } = string.Empty;

    [MessageBodyMember]
    public string VehicleModel { get; set; } = string.Empty;

    [MessageBodyMember]
    public int VehicleYear { get; set; }
}

[MessageContract]
public class GetQuotesByCustomerRequest
{
    [MessageBodyMember]
    public int CustomerId { get; set; }
}

[MessageContract]
public class GetQuotesByCustomerResponse
{
    [MessageBodyMember]
    public QuoteResponse[] Quotes { get; set; } = Array.Empty<QuoteResponse>();
}

[MessageContract]
public class ApproveQuoteRequest
{
    [MessageBodyMember]
    public string QuoteNumber { get; set; } = string.Empty;
}

[MessageContract]
public class ApproveQuoteResponse
{
    [MessageBodyMember]
    public bool Success { get; set; }
}

[MessageContract]
public class QuoteResponse
{
    [MessageBodyMember]
    public string QuoteNumber { get; set; } = string.Empty;

    [MessageBodyMember]
    public int CustomerId { get; set; }

    [MessageBodyMember]
    public decimal Premium { get; set; }

    [MessageBodyMember]
    public DateTime ValidUntil { get; set; }

    [MessageBodyMember]
    public string Status { get; set; } = string.Empty;
}

