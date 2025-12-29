using System.ServiceModel;

namespace Legacy.TestClient;

// Contratos simplificados para teste
[ServiceContract(Namespace = "http://eximia.co/seguroauto/legacy")]
public interface IQuoteService
{
    [OperationContract]
    QuoteResponse GetQuote(QuoteRequest request);
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

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Legacy Test Client - Seguro Auto");
        Console.WriteLine("================================\n");

        var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

        try
        {
            var binding = new BasicHttpBinding();
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

