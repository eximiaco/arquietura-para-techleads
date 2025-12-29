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
            var soapBody = $@"<legacy:GetAllRulesRequest />";

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
            var soapBody = $@"<legacy:GetRuleRequest>
                <legacy:RuleId>{ruleId}</legacy:RuleId>
            </legacy:GetRuleRequest>";

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
            var soapBody = $@"<legacy:UpdateRuleRequest>
                <legacy:Id>{id}</legacy:Id>
                <legacy:IsActive>{isActive.ToString().ToLower()}</legacy:IsActive>
            </legacy:UpdateRuleRequest>";

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

    private List<PricingRuleResponse> ParseGetAllRulesResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        // Tentar com namespace primeiro, depois sem namespace (fallback)
        var response = body?.Descendants(legacyNs + "GetAllRulesResponse").FirstOrDefault()
                    ?? body?.Descendants().FirstOrDefault(e => e.Name.LocalName == "GetAllRulesResponse");
        
        if (response == null)
            throw new InvalidOperationException("GetAllRulesResponse not found in SOAP response");
        
        // Tentar com namespace primeiro, depois sem namespace (fallback)
        var rules = response.Descendants(legacyNs + "PricingRuleResponse").Any()
            ? response.Descendants(legacyNs + "PricingRuleResponse")
            : response.Descendants().Where(e => e.Name.LocalName == "PricingRuleResponse");

        return rules.Select(r => new PricingRuleResponse
        {
            Id = int.Parse(r.Element(legacyNs + "Id")?.Value 
                    ?? r.Descendants().FirstOrDefault(e => e.Name.LocalName == "Id")?.Value 
                    ?? "0"),
            Name = r.Element(legacyNs + "Name")?.Value 
                ?? r.Descendants().FirstOrDefault(e => e.Name.LocalName == "Name")?.Value 
                ?? string.Empty,
            Description = r.Element(legacyNs + "Description")?.Value 
                       ?? r.Descendants().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value 
                       ?? string.Empty,
            Multiplier = decimal.Parse(r.Element(legacyNs + "Multiplier")?.Value 
                            ?? r.Descendants().FirstOrDefault(e => e.Name.LocalName == "Multiplier")?.Value 
                            ?? "1"),
            IsActive = bool.Parse(r.Element(legacyNs + "IsActive")?.Value 
                       ?? r.Descendants().FirstOrDefault(e => e.Name.LocalName == "IsActive")?.Value 
                       ?? "false")
        }).ToList();
    }

    private PricingRuleResponse ParseGetRuleResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        // Tentar com namespace primeiro, depois sem namespace (fallback)
        var ruleResponse = body?.Descendants(legacyNs + "PricingRuleResponse").FirstOrDefault()
                        ?? body?.Descendants().FirstOrDefault(e => e.Name.LocalName == "PricingRuleResponse");

        if (ruleResponse == null)
            throw new InvalidOperationException("Invalid SOAP response format");

        // Tentar com namespace primeiro, depois sem namespace (fallback)
        return new PricingRuleResponse
        {
            Id = int.Parse(ruleResponse.Element(legacyNs + "Id")?.Value 
                    ?? ruleResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "Id")?.Value 
                    ?? "0"),
            Name = ruleResponse.Element(legacyNs + "Name")?.Value 
                ?? ruleResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "Name")?.Value 
                ?? string.Empty,
            Description = ruleResponse.Element(legacyNs + "Description")?.Value 
                       ?? ruleResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value 
                       ?? string.Empty,
            Multiplier = decimal.Parse(ruleResponse.Element(legacyNs + "Multiplier")?.Value 
                            ?? ruleResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "Multiplier")?.Value 
                            ?? "1"),
            IsActive = bool.Parse(ruleResponse.Element(legacyNs + "IsActive")?.Value 
                       ?? ruleResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "IsActive")?.Value 
                       ?? "false")
        };
    }

    private bool ParseUpdateRuleResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);
        
        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        // Tentar com namespace primeiro, depois sem namespace (fallback)
        var response = body?.Descendants(legacyNs + "UpdateRuleResponse").FirstOrDefault()
                    ?? body?.Descendants().FirstOrDefault(e => e.Name.LocalName == "UpdateRuleResponse");
        
        if (response == null)
            throw new InvalidOperationException("UpdateRuleResponse not found in SOAP response");
        
        var success = response.Element(legacyNs + "Success")?.Value 
                   ?? response.Descendants().FirstOrDefault(e => e.Name.LocalName == "Success")?.Value 
                   ?? "false";

        return bool.Parse(success);
    }
}

