using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SeguroAuto.Web.Services;

public class QuoteServiceClient : IQuoteServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QuoteServiceClient> _logger;
    private readonly string _gatewayUrl;
    private const string Namespace = "http://eximia.co/seguroauto/legacy";

    public QuoteServiceClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<QuoteServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        
        // Obtém URL do gateway via service discovery do Aspire
        _gatewayUrl = _configuration["services__gateway__http__0"] 
                   ?? _configuration["Gateway:Url"] 
                   ?? "http://localhost:5000";
    }

    public async Task<QuoteResponse> GetQuoteAsync(int customerId, string vehiclePlate, string vehicleModel, int vehicleYear)
    {
        try
        {
            var soapBody = $@"<GetQuoteRequest xmlns=""{Namespace}"">
                <CustomerId>{customerId}</CustomerId>
                <VehiclePlate>{EscapeXml(vehiclePlate)}</VehiclePlate>
                <VehicleModel>{EscapeXml(vehicleModel)}</VehicleModel>
                <VehicleYear>{vehicleYear}</VehicleYear>
            </GetQuoteRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/QuoteService.svc", "IQuoteService/GetQuote", soapEnvelope);
            
            return ParseQuoteResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GetQuote for CustomerId: {CustomerId}", customerId);
            throw;
        }
    }

    public async Task<List<QuoteResponse>> GetQuotesByCustomerAsync(int customerId)
    {
        try
        {
            var soapBody = $@"<GetQuotesByCustomerRequest xmlns=""{Namespace}"">
                <CustomerId>{customerId}</CustomerId>
            </GetQuotesByCustomerRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/QuoteService.svc", "IQuoteService/GetQuotesByCustomer", soapEnvelope);
            
            return ParseQuotesByCustomerResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GetQuotesByCustomer for CustomerId: {CustomerId}", customerId);
            throw;
        }
    }

    public async Task<bool> ApproveQuoteAsync(string quoteNumber)
    {
        try
        {
            var soapBody = $@"<ApproveQuoteRequest xmlns=""{Namespace}"">
                <QuoteNumber>{EscapeXml(quoteNumber)}</QuoteNumber>
            </ApproveQuoteRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/QuoteService.svc", "IQuoteService/ApproveQuote", soapEnvelope);
            
            return ParseApproveQuoteResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling ApproveQuote for QuoteNumber: {QuoteNumber}", quoteNumber);
            throw;
        }
    }

    private string BuildSoapEnvelope(string body)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
    <s:Body>
        {body}
    </s:Body>
</s:Envelope>";
    }

    private async Task<string> SendSoapRequestAsync(string endpoint, string soapAction, string soapEnvelope)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var url = $"{_gatewayUrl.TrimEnd('/')}{endpoint}";
        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml; charset=utf-8");
        content.Headers.Add("SOAPAction", $"{Namespace}/{soapAction}");

        var response = await httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private QuoteResponse ParseQuoteResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var quoteResponse = body?.Descendants(legacyNs + "QuoteResponse").FirstOrDefault();

        if (quoteResponse == null)
            throw new InvalidOperationException("Invalid SOAP response format");

        return new QuoteResponse
        {
            QuoteNumber = quoteResponse.Element(legacyNs + "QuoteNumber")?.Value ?? string.Empty,
            CustomerId = int.Parse(quoteResponse.Element(legacyNs + "CustomerId")?.Value ?? "0"),
            Premium = decimal.Parse(quoteResponse.Element(legacyNs + "Premium")?.Value ?? "0"),
            ValidUntil = DateTime.Parse(quoteResponse.Element(legacyNs + "ValidUntil")?.Value ?? DateTime.UtcNow.ToString()),
            Status = quoteResponse.Element(legacyNs + "Status")?.Value ?? string.Empty
        };
    }

    private List<QuoteResponse> ParseQuotesByCustomerResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var response = body?.Descendants(legacyNs + "GetQuotesByCustomerResponse").FirstOrDefault();
        var quotes = response?.Descendants(legacyNs + "QuoteResponse") ?? Enumerable.Empty<XElement>();

        return quotes.Select(q => new QuoteResponse
        {
            QuoteNumber = q.Element(legacyNs + "QuoteNumber")?.Value ?? string.Empty,
            CustomerId = int.Parse(q.Element(legacyNs + "CustomerId")?.Value ?? "0"),
            Premium = decimal.Parse(q.Element(legacyNs + "Premium")?.Value ?? "0"),
            ValidUntil = DateTime.Parse(q.Element(legacyNs + "ValidUntil")?.Value ?? DateTime.UtcNow.ToString()),
            Status = q.Element(legacyNs + "Status")?.Value ?? string.Empty
        }).ToList();
    }

    private bool ParseApproveQuoteResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var response = body?.Descendants(legacyNs + "ApproveQuoteResponse").FirstOrDefault();
        var success = response?.Element(legacyNs + "Success")?.Value ?? "false";

        return bool.Parse(success);
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&apos;");
    }
}

