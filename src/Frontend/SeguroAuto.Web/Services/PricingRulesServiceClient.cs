using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SeguroAuto.Web.Services;

public class PricingRulesServiceClient : IPricingRulesServiceClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PricingRulesServiceClient> _logger;
    private readonly string _gatewayUrl;
    private const string Namespace = "http://eximia.co/seguroauto/legacy";

    public PricingRulesServiceClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PricingRulesServiceClient> logger)
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

    public async Task<List<PricingRuleResponse>> GetAllRulesAsync()
    {
        try
        {
            var soapBody = $@"<GetAllRulesRequest xmlns=""{Namespace}"" />";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/PricingRulesService.svc", "IPricingRulesService/GetAllRules", soapEnvelope);
            
            return ParseGetAllRulesResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GetAllRules");
            throw;
        }
    }

    public async Task<PricingRuleResponse> GetRuleAsync(int ruleId)
    {
        try
        {
            var soapBody = $@"<GetRuleRequest xmlns=""{Namespace}"">
                <RuleId>{ruleId}</RuleId>
            </GetRuleRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/PricingRulesService.svc", "IPricingRulesService/GetRule", soapEnvelope);
            
            return ParseGetRuleResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling GetRule for RuleId: {RuleId}", ruleId);
            throw;
        }
    }

    public async Task<bool> UpdateRuleAsync(int id, bool isActive)
    {
        try
        {
            var soapBody = $@"<UpdateRuleRequest xmlns=""{Namespace}"">
                <Id>{id}</Id>
                <IsActive>{isActive.ToString().ToLower()}</IsActive>
            </UpdateRuleRequest>";

            var soapEnvelope = BuildSoapEnvelope(soapBody);
            var response = await SendSoapRequestAsync("/PricingRulesService.svc", "IPricingRulesService/UpdateRule", soapEnvelope);
            
            return ParseUpdateRuleResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling UpdateRule for Id: {Id}", id);
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

    private List<PricingRuleResponse> ParseGetAllRulesResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var response = body?.Descendants(legacyNs + "GetAllRulesResponse").FirstOrDefault();
        var rules = response?.Descendants(legacyNs + "PricingRuleResponse") ?? Enumerable.Empty<XElement>();

        return rules.Select(r => new PricingRuleResponse
        {
            Id = int.Parse(r.Element(legacyNs + "Id")?.Value ?? "0"),
            Name = r.Element(legacyNs + "Name")?.Value ?? string.Empty,
            Description = r.Element(legacyNs + "Description")?.Value ?? string.Empty,
            Multiplier = decimal.Parse(r.Element(legacyNs + "Multiplier")?.Value ?? "1"),
            IsActive = bool.Parse(r.Element(legacyNs + "IsActive")?.Value ?? "false")
        }).ToList();
    }

    private PricingRuleResponse ParseGetRuleResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var ruleResponse = body?.Descendants(legacyNs + "PricingRuleResponse").FirstOrDefault();

        if (ruleResponse == null)
            throw new InvalidOperationException("Invalid SOAP response format");

        return new PricingRuleResponse
        {
            Id = int.Parse(ruleResponse.Element(legacyNs + "Id")?.Value ?? "0"),
            Name = ruleResponse.Element(legacyNs + "Name")?.Value ?? string.Empty,
            Description = ruleResponse.Element(legacyNs + "Description")?.Value ?? string.Empty,
            Multiplier = decimal.Parse(ruleResponse.Element(legacyNs + "Multiplier")?.Value ?? "1"),
            IsActive = bool.Parse(ruleResponse.Element(legacyNs + "IsActive")?.Value ?? "false")
        };
    }

    private bool ParseUpdateRuleResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var response = body?.Descendants(legacyNs + "UpdateRuleResponse").FirstOrDefault();
        var success = response?.Element(legacyNs + "Success")?.Value ?? "false";

        return bool.Parse(success);
    }
}

