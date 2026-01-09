using System.ServiceModel;
using System.ServiceModel.Channels;
using CoreWCF;
using EndpointAddress = System.ServiceModel.EndpointAddress;
using IClientChannel = System.ServiceModel.IClientChannel;

namespace Legacy.TestClient;

// Contratos simplificados para teste
[System.ServiceModel.ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IQuoteService
{
    [System.ServiceModel.OperationContract]
    QuoteResponse GetQuote(QuoteRequest request);

    [System.ServiceModel.OperationContract]
    GetQuotesByCustomerResponse GetQuotesByCustomer(GetQuotesByCustomerRequest request);
}

[System.ServiceModel.MessageContract]
public class QuoteRequest
{
    [System.ServiceModel.MessageBodyMember]
    public int CustomerId { get; set; }

    [System.ServiceModel.MessageBodyMember]
    public string VehiclePlate { get; set; } = string.Empty;

    [System.ServiceModel.MessageBodyMember]
    public string VehicleModel { get; set; } = string.Empty;

    [System.ServiceModel.MessageBodyMember]
    public int VehicleYear { get; set; }
}

[System.ServiceModel.MessageContract]
public class GetQuotesByCustomerRequest
{
    [System.ServiceModel.MessageBodyMember]
    public int CustomerId { get; set; }
}

[System.ServiceModel.MessageContract]
public class GetQuotesByCustomerResponse
{
    [System.ServiceModel.MessageBodyMember]
    public QuoteResponse[] Quotes { get; set; } = Array.Empty<QuoteResponse>();
}

[System.ServiceModel.MessageContract]
public class QuoteResponse
{
    [System.ServiceModel.MessageBodyMember]
    public string QuoteNumber { get; set; } = string.Empty;

    [System.ServiceModel.MessageBodyMember]
    public int CustomerId { get; set; }

    [System.ServiceModel.MessageBodyMember]
    public decimal Premium { get; set; }

    [System.ServiceModel.MessageBodyMember]
    public DateTime ValidUntil { get; set; }

    [System.ServiceModel.MessageBodyMember]
    public string Status { get; set; } = string.Empty;
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Legacy Test Client - Seguro Auto");
        Console.WriteLine("================================\n");

        var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

        try
        {
            var binding = new CustomBinding();
            var endpoint = new EndpointAddress($"{baseUrl}/QuoteService.svc");
            var factory = new ChannelFactory<IQuoteService>(binding, endpoint);
            var client = factory.CreateChannel();

            var request = new QuoteRequest
            {
                CustomerId = 999, // ID âncora
                VehiclePlate = "ABC-1234",
                VehicleModel = "Honda Civic",
                VehicleYear = 2020
            };

            Console.WriteLine($"Calling GetQuote for CustomerId: {request.CustomerId}");
            var response = client.GetQuote(request);
            
            // Exemplo de chamada com GetQuotesByCustomer
            var quotesRequest = new GetQuotesByCustomerRequest { CustomerId = 999 };
            var quotesResponse = client.GetQuotesByCustomer(quotesRequest);
            Console.WriteLine($"Found {quotesResponse.Quotes.Length} quotes for customer");

            Console.WriteLine($"Quote Number: {response.QuoteNumber}");
            Console.WriteLine($"Premium: {response.Premium:C}");
            Console.WriteLine($"Valid Until: {response.ValidUntil}");
            Console.WriteLine($"Status: {response.Status}");

            ((IClientChannel)client).Close();
            factory.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
        }
    }
}

