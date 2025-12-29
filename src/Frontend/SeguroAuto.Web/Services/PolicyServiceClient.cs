using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SeguroAuto.Web.Services;

public class PolicyServiceClient : IPolicyServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PolicyServiceClient> _logger;
    private readonly string _gatewayUrl;
    private const string Namespace = "http://eximia.co/seguroauto/legacy";

    public PolicyServiceClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PolicyServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        
        _gatewayUrl = _configuration["services__gateway__http__0"] 
                   ?? _configuration["Gateway:Url"] 
                   ?? "http://localhost:5000";
    }

    public async Task<PolicyResponse> GetPolicyAsync(string policyNumber)
    {
        try
        {
            var soapBody = $@"<GetPolicyRequest xmlns=""{Namespace}"">
                <PolicyNumber>{EscapeXml(policyNumber)}</PolicyNumber>
            </GetPolicyRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/PolicyService.svc", "IPolicyService/GetPolicy", soapEnvelope);
            
            return ParsePolicyResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GetPolicy for PolicyNumber: {PolicyNumber}", policyNumber);
            throw;
        }
    }

    public async Task<List<PolicyResponse>> GetPoliciesByCustomerAsync(int customerId)
    {
        try
        {
            var soapBody = $@"<GetPoliciesByCustomerRequest xmlns=""{Namespace}"">
                <CustomerId>{customerId}</CustomerId>
            </GetPoliciesByCustomerRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/PolicyService.svc", "IPolicyService/GetPoliciesByCustomer", soapEnvelope);
            
            return ParsePoliciesByCustomerResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GetPoliciesByCustomer for CustomerId: {CustomerId}", customerId);
            throw;
        }
    }

    public async Task<PolicyResponse> CreatePolicyAsync(int customerId, string vehiclePlate, string vehicleModel, int vehicleYear, decimal premium)
    {
        try
        {
            var soapBody = $@"<CreatePolicyRequest xmlns=""{Namespace}"">
                <CustomerId>{customerId}</CustomerId>
                <VehiclePlate>{EscapeXml(vehiclePlate)}</VehiclePlate>
                <VehicleModel>{EscapeXml(vehicleModel)}</VehicleModel>
                <VehicleYear>{vehicleYear}</VehicleYear>
                <Premium>{premium}</Premium>
            </CreatePolicyRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/PolicyService.svc", "IPolicyService/CreatePolicy", soapEnvelope);
            
            return ParsePolicyResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling CreatePolicy for CustomerId: {CustomerId}", customerId);
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

    private PolicyResponse ParsePolicyResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var policyResponse = body?.Descendants(legacyNs + "PolicyResponse").FirstOrDefault();

        if (policyResponse == null)
            throw new InvalidOperationException("Invalid SOAP response format");

        return new PolicyResponse
        {
            PolicyNumber = policyResponse.Element(legacyNs + "PolicyNumber")?.Value ?? string.Empty,
            CustomerId = int.Parse(policyResponse.Element(legacyNs + "CustomerId")?.Value ?? "0"),
            VehiclePlate = policyResponse.Element(legacyNs + "VehiclePlate")?.Value ?? string.Empty,
            Premium = decimal.Parse(policyResponse.Element(legacyNs + "Premium")?.Value ?? "0"),
            StartDate = DateTime.Parse(policyResponse.Element(legacyNs + "StartDate")?.Value ?? DateTime.UtcNow.ToString()),
            EndDate = DateTime.Parse(policyResponse.Element(legacyNs + "EndDate")?.Value ?? DateTime.UtcNow.ToString()),
            Status = policyResponse.Element(legacyNs + "Status")?.Value ?? string.Empty
        };
    }

    private List<PolicyResponse> ParsePoliciesByCustomerResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var response = body?.Descendants(legacyNs + "GetPoliciesByCustomerResponse").FirstOrDefault();
        var policies = response?.Descendants(legacyNs + "PolicyResponse") ?? Enumerable.Empty<XElement>();

        return policies.Select(p => new PolicyResponse
        {
            PolicyNumber = p.Element(legacyNs + "PolicyNumber")?.Value ?? string.Empty,
            CustomerId = int.Parse(p.Element(legacyNs + "CustomerId")?.Value ?? "0"),
            VehiclePlate = p.Element(legacyNs + "VehiclePlate")?.Value ?? string.Empty,
            Premium = decimal.Parse(p.Element(legacyNs + "Premium")?.Value ?? "0"),
            StartDate = DateTime.Parse(p.Element(legacyNs + "StartDate")?.Value ?? DateTime.UtcNow.ToString()),
            EndDate = DateTime.Parse(p.Element(legacyNs + "EndDate")?.Value ?? DateTime.UtcNow.ToString()),
            Status = p.Element(legacyNs + "Status")?.Value ?? string.Empty
        }).ToList();
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

