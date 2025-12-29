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
            var soapBody = $@"<legacy:GetClaimRequest>
                <legacy:ClaimNumber>{EscapeXml(claimNumber)}</legacy:ClaimNumber>
            </legacy:GetClaimRequest>";

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
            var soapBody = $@"<legacy:GetClaimsByPolicyRequest>
                <legacy:PolicyNumber>{EscapeXml(policyNumber)}</legacy:PolicyNumber>
            </legacy:GetClaimsByPolicyRequest>";

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
            var soapBody = $@"<legacy:CreateClaimRequest>
                <legacy:PolicyNumber>{EscapeXml(policyNumber)}</legacy:PolicyNumber>
                <legacy:Description>{EscapeXml(description)}</legacy:Description>
                <legacy:Amount>{amount}</legacy:Amount>
                <legacy:IncidentDate>{incidentDate:yyyy-MM-ddTHH:mm:ssZ}</legacy:IncidentDate>
            </legacy:CreateClaimRequest>";

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
        // Usar o formato EXATO do Legacy-Services.http que funciona
        // O namespace legacy deve ser definido no Envelope, não no body
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:legacy=""{Namespace}"">
    <soap:Body>
        {body}
    </soap:Body>
</soap:Envelope>";
    }

    private async Task<string> SendSoapRequestAsync(string endpoint, string soapAction, string soapEnvelope)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"{_gatewayUrl.TrimEnd('/')}{endpoint}";
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", $"{Namespace}/{soapAction}");

            var response = await httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("SOAP request failed with status {StatusCode}. Response: {Response}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when calling SOAP endpoint {Endpoint}", endpoint);
            throw;
        }
    }

    private ClaimResponse ParseClaimResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        // Tentar com namespace primeiro, depois sem namespace (fallback)
        var claimResponse = body?.Descendants(legacyNs + "ClaimResponse").FirstOrDefault()
                        ?? body?.Descendants().FirstOrDefault(e => e.Name.LocalName == "ClaimResponse");

        if (claimResponse == null)
            throw new InvalidOperationException("Invalid SOAP response format");

        // Tentar com namespace primeiro, depois sem namespace (fallback)
        return new ClaimResponse
        {
            ClaimNumber = claimResponse.Element(legacyNs + "ClaimNumber")?.Value 
                       ?? claimResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "ClaimNumber")?.Value 
                       ?? string.Empty,
            PolicyNumber = claimResponse.Element(legacyNs + "PolicyNumber")?.Value 
                        ?? claimResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "PolicyNumber")?.Value 
                        ?? string.Empty,
            Description = claimResponse.Element(legacyNs + "Description")?.Value 
                       ?? claimResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value 
                       ?? string.Empty,
            Amount = decimal.Parse(claimResponse.Element(legacyNs + "Amount")?.Value 
                     ?? claimResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "Amount")?.Value 
                     ?? "0"),
            Status = claimResponse.Element(legacyNs + "Status")?.Value 
                  ?? claimResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "Status")?.Value 
                  ?? string.Empty,
            IncidentDate = DateTime.Parse(claimResponse.Element(legacyNs + "IncidentDate")?.Value 
                           ?? claimResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "IncidentDate")?.Value 
                           ?? DateTime.UtcNow.ToString())
        };
    }

    private List<ClaimResponse> ParseClaimsByPolicyResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        // Tentar com namespace primeiro, depois sem namespace (fallback)
        var response = body?.Descendants(legacyNs + "GetClaimsByPolicyResponse").FirstOrDefault()
                    ?? body?.Descendants().FirstOrDefault(e => e.Name.LocalName == "GetClaimsByPolicyResponse");
        
        if (response == null)
            throw new InvalidOperationException("GetClaimsByPolicyResponse not found in SOAP response");
        
        // Tentar com namespace primeiro, depois sem namespace (fallback)
        var claims = response.Descendants(legacyNs + "ClaimResponse").Any()
            ? response.Descendants(legacyNs + "ClaimResponse")
            : response.Descendants().Where(e => e.Name.LocalName == "ClaimResponse");

        return claims.Select(c => new ClaimResponse
        {
            ClaimNumber = c.Element(legacyNs + "ClaimNumber")?.Value 
                       ?? c.Descendants().FirstOrDefault(e => e.Name.LocalName == "ClaimNumber")?.Value 
                       ?? string.Empty,
            PolicyNumber = c.Element(legacyNs + "PolicyNumber")?.Value 
                        ?? c.Descendants().FirstOrDefault(e => e.Name.LocalName == "PolicyNumber")?.Value 
                        ?? string.Empty,
            Description = c.Element(legacyNs + "Description")?.Value 
                       ?? c.Descendants().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value 
                       ?? string.Empty,
            Amount = decimal.Parse(c.Element(legacyNs + "Amount")?.Value 
                     ?? c.Descendants().FirstOrDefault(e => e.Name.LocalName == "Amount")?.Value 
                     ?? "0"),
            Status = c.Element(legacyNs + "Status")?.Value 
                  ?? c.Descendants().FirstOrDefault(e => e.Name.LocalName == "Status")?.Value 
                  ?? string.Empty,
            IncidentDate = DateTime.Parse(c.Element(legacyNs + "IncidentDate")?.Value 
                           ?? c.Descendants().FirstOrDefault(e => e.Name.LocalName == "IncidentDate")?.Value 
                           ?? DateTime.UtcNow.ToString())
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

