using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SeguroAuto.Web.Services;

public class ClaimsServiceClient : IClaimsServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClaimsServiceClient> _logger;
    private readonly string _gatewayUrl;
    private const string Namespace = "http://eximia.co/seguroauto/legacy";

    public ClaimsServiceClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ClaimsServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        
        // Obtém URL do gateway via service discovery do Aspire
        _gatewayUrl = _configuration["services__gateway__http__0"] 
                   ?? Environment.GetEnvironmentVariable("services__gateway__http__0")
                   ?? _configuration["Gateway:Url"] 
                   ?? Environment.GetEnvironmentVariable("Gateway__Url")
                   ?? "http://localhost:5000";
    }

    public async Task<ClaimResponse> GetClaimAsync(string claimNumber)
    {
        try
        {
            var soapBody = $@"<GetClaimRequest xmlns=""{Namespace}"">
                <ClaimNumber>{EscapeXml(claimNumber)}</ClaimNumber>
            </GetClaimRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/ClaimsService.svc", "IClaimsService/GetClaim", soapEnvelope);
            
            return ParseClaimResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GetClaim for ClaimNumber: {ClaimNumber}", claimNumber);
            throw;
        }
    }

    public async Task<List<ClaimResponse>> GetClaimsByPolicyAsync(string policyNumber)
    {
        try
        {
            var soapBody = $@"<GetClaimsByPolicyRequest xmlns=""{Namespace}"">
                <PolicyNumber>{EscapeXml(policyNumber)}</PolicyNumber>
            </GetClaimsByPolicyRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/ClaimsService.svc", "IClaimsService/GetClaimsByPolicy", soapEnvelope);
            
            return ParseClaimsByPolicyResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GetClaimsByPolicy for PolicyNumber: {PolicyNumber}", policyNumber);
            throw;
        }
    }

    public async Task<ClaimResponse> CreateClaimAsync(string policyNumber, string description, decimal amount, DateTime incidentDate)
    {
        try
        {
            var soapBody = $@"<CreateClaimRequest xmlns=""{Namespace}"">
                <PolicyNumber>{EscapeXml(policyNumber)}</PolicyNumber>
                <Description>{EscapeXml(description)}</Description>
                <Amount>{amount}</Amount>
                <IncidentDate>{incidentDate:yyyy-MM-ddTHH:mm:ssZ}</IncidentDate>
            </CreateClaimRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/ClaimsService.svc", "IClaimsService/CreateClaim", soapEnvelope);
            
            return ParseClaimResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling CreateClaim for PolicyNumber: {PolicyNumber}", policyNumber);
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
        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", $"{Namespace}/{soapAction}");

        var response = await httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private ClaimResponse ParseClaimResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var claimResponse = body?.Descendants(legacyNs + "ClaimResponse").FirstOrDefault();

        if (claimResponse == null)
            throw new InvalidOperationException("Invalid SOAP response format");

        return new ClaimResponse
        {
            ClaimNumber = claimResponse.Element(legacyNs + "ClaimNumber")?.Value ?? string.Empty,
            PolicyNumber = claimResponse.Element(legacyNs + "PolicyNumber")?.Value ?? string.Empty,
            Description = claimResponse.Element(legacyNs + "Description")?.Value ?? string.Empty,
            Amount = decimal.Parse(claimResponse.Element(legacyNs + "Amount")?.Value ?? "0"),
            Status = claimResponse.Element(legacyNs + "Status")?.Value ?? string.Empty,
            IncidentDate = DateTime.Parse(claimResponse.Element(legacyNs + "IncidentDate")?.Value ?? DateTime.UtcNow.ToString())
        };
    }

    private List<ClaimResponse> ParseClaimsByPolicyResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var response = body?.Descendants(legacyNs + "GetClaimsByPolicyResponse").FirstOrDefault();
        var claims = response?.Descendants(legacyNs + "ClaimResponse") ?? Enumerable.Empty<XElement>();

        return claims.Select(c => new ClaimResponse
        {
            ClaimNumber = c.Element(legacyNs + "ClaimNumber")?.Value ?? string.Empty,
            PolicyNumber = c.Element(legacyNs + "PolicyNumber")?.Value ?? string.Empty,
            Description = c.Element(legacyNs + "Description")?.Value ?? string.Empty,
            Amount = decimal.Parse(c.Element(legacyNs + "Amount")?.Value ?? "0"),
            Status = c.Element(legacyNs + "Status")?.Value ?? string.Empty,
            IncidentDate = DateTime.Parse(c.Element(legacyNs + "IncidentDate")?.Value ?? DateTime.UtcNow.ToString())
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

