using System.ServiceModel;

namespace Legacy.QuoteService;

[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IQuoteService
{
    [OperationContract]
    QuoteResponse GetQuote(QuoteRequest request);

    [OperationContract]
    QuoteResponse[] GetQuotesByCustomer(int customerId);

    [OperationContract]
    bool ApproveQuote(string quoteNumber);
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

