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
        // O Aspire injeta variáveis de ambiente no formato: services__gateway__http__0
        // Tentar primeiro via IConfiguration (que lê variáveis de ambiente), depois diretamente
        _gatewayUrl = _configuration["services__gateway__http__0"] 
                   ?? Environment.GetEnvironmentVariable("services__gateway__http__0")
                   ?? _configuration["Gateway:Url"] 
                   ?? Environment.GetEnvironmentVariable("Gateway__Url")
                   ?? "http://localhost:5000";
        
        // Log de diagnóstico: listar variáveis relacionadas ao gateway
        var gatewayVars = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(e => e.Key?.ToString()?.Contains("gateway", StringComparison.OrdinalIgnoreCase) == true)
            .Select(e => $"{e.Key}={e.Value}")
            .ToList();
        
        if (gatewayVars.Any())
        {
            _logger.LogInformation("Gateway-related environment variables found: {Vars}", string.Join(", ", gatewayVars));
        }
        else
        {
            _logger.LogWarning("No gateway environment variables found. Using fallback URL: {GatewayUrl}", _gatewayUrl);
        }
        
        _logger.LogInformation("QuoteServiceClient initialized with Gateway URL: {GatewayUrl}", _gatewayUrl);
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
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"{_gatewayUrl.TrimEnd('/')}{endpoint}";
            
            _logger.LogDebug("Sending SOAP request to {Url} with action {SoapAction}", url, soapAction);
            
            // StringContent: o charset é definido pelo Encoding, não no mediaType
            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", $"{Namespace}/{soapAction}");

            var response = await httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("SOAP request failed with status {StatusCode}. URL: {Url}, SOAPAction: {SoapAction}. Response: {Response}", 
                    response.StatusCode, url, soapAction, errorContent);
                
                // Para erro 500, tentar extrair mensagem de erro do SOAP Fault se possível
                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError && errorContent.Contains("Fault"))
                {
                    try
                    {
                        var faultDoc = XDocument.Parse(errorContent);
                        var faultString = faultDoc.Descendants()
                            .FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value
                         ?? faultDoc.Descendants()
                            .FirstOrDefault(e => e.Name.LocalName == "FaultString")?.Value;
                        
                        if (!string.IsNullOrEmpty(faultString))
                        {
                            _logger.LogError("SOAP Fault message: {FaultMessage}", faultString);
                            throw new InvalidOperationException($"SOAP service error: {faultString}");
                        }
                    }
                    catch
                    {
                        // Se não conseguir parsear o fault, continua com o erro genérico
                    }
                }
                
                response.EnsureSuccessStatusCode();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("SOAP response received (length: {Length}): {Response}", 
                responseContent.Length, responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);
            
            return responseContent;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error when calling SOAP endpoint {Endpoint}", endpoint);
            throw new InvalidOperationException($"Failed to call SOAP service at {_gatewayUrl}{endpoint}. Check if the gateway is running and accessible.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when calling SOAP endpoint {Endpoint}", endpoint);
            throw;
        }
    }

    private QuoteResponse ParseQuoteResponse(string xml)
    {
        try
        {
            _logger.LogDebug("Parsing QuoteResponse XML (length: {Length})", xml.Length);
            
            var doc = XDocument.Parse(xml);
            var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
            var legacyNs = XNamespace.Get(Namespace);
            
            var body = doc.Descendants(ns + "Body").FirstOrDefault();
            if (body == null)
            {
                _logger.LogError("SOAP Body not found in response. XML: {Xml}", xml);
                throw new InvalidOperationException("SOAP Body not found in response");
            }
            
            // Tentar encontrar QuoteResponse com namespace explícito ou sem namespace
            var quoteResponse = body.Descendants(legacyNs + "QuoteResponse").FirstOrDefault()
                             ?? body.Descendants().FirstOrDefault(e => e.Name.LocalName == "QuoteResponse");

            if (quoteResponse == null)
            {
                _logger.LogError("QuoteResponse not found in SOAP Body. Available elements: {Elements}", 
                    string.Join(", ", body.Descendants().Select(e => e.Name.LocalName)));
                throw new InvalidOperationException("QuoteResponse not found in SOAP response");
            }

            // Tentar com namespace primeiro, depois sem namespace
            var quoteNumber = quoteResponse.Element(legacyNs + "QuoteNumber")?.Value 
                           ?? quoteResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "QuoteNumber")?.Value 
                           ?? string.Empty;
            
            var customerIdStr = quoteResponse.Element(legacyNs + "CustomerId")?.Value 
                             ?? quoteResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "CustomerId")?.Value 
                             ?? "0";
            
            var premiumStr = quoteResponse.Element(legacyNs + "Premium")?.Value 
                          ?? quoteResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "Premium")?.Value 
                          ?? "0";
            
            var validUntilStr = quoteResponse.Element(legacyNs + "ValidUntil")?.Value 
                             ?? quoteResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "ValidUntil")?.Value 
                             ?? DateTime.UtcNow.ToString();
            
            var status = quoteResponse.Element(legacyNs + "Status")?.Value 
                      ?? quoteResponse.Descendants().FirstOrDefault(e => e.Name.LocalName == "Status")?.Value 
                      ?? string.Empty;

            var result = new QuoteResponse
            {
                QuoteNumber = quoteNumber,
                CustomerId = int.Parse(customerIdStr),
                Premium = decimal.Parse(premiumStr),
                ValidUntil = DateTime.Parse(validUntilStr),
                Status = status
            };
            
            _logger.LogInformation("Successfully parsed QuoteResponse: {QuoteNumber}", result.QuoteNumber);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing QuoteResponse. XML: {Xml}", xml);
            throw;
        }
    }

    private List<QuoteResponse> ParseQuotesByCustomerResponse(string xml)
    {
        try
        {
            _logger.LogDebug("Parsing SOAP response XML (length: {Length})", xml.Length);
            
            var doc = XDocument.Parse(xml);
            var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
            var legacyNs = XNamespace.Get(Namespace);
            
            var body = doc.Descendants(ns + "Body").FirstOrDefault();
            if (body == null)
            {
                _logger.LogError("SOAP Body not found in response. XML: {Xml}", xml);
                throw new InvalidOperationException("SOAP Body not found in response");
            }
            
            // Tentar encontrar a resposta com namespace explícito ou sem namespace
            var response = body.Descendants(legacyNs + "GetQuotesByCustomerResponse").FirstOrDefault()
                         ?? body.Descendants().FirstOrDefault(e => e.Name.LocalName == "GetQuotesByCustomerResponse");
            
            if (response == null)
            {
                _logger.LogError("GetQuotesByCustomerResponse not found in SOAP Body. Available elements: {Elements}", 
                    string.Join(", ", body.Descendants().Select(e => e.Name.LocalName)));
                throw new InvalidOperationException("GetQuotesByCustomerResponse not found in SOAP response");
            }
            
            // Tentar encontrar quotes com namespace explícito ou sem namespace
            var quotes = response.Descendants(legacyNs + "QuoteResponse").Any() 
                ? response.Descendants(legacyNs + "QuoteResponse")
                : response.Descendants().Where(e => e.Name.LocalName == "QuoteResponse");

            var quoteList = quotes.Select(q => 
            {
                // Tentar com namespace primeiro, depois sem namespace
                var quoteNumber = q.Element(legacyNs + "QuoteNumber")?.Value 
                               ?? q.Descendants().FirstOrDefault(e => e.Name.LocalName == "QuoteNumber")?.Value 
                               ?? string.Empty;
                
                var customerIdStr = q.Element(legacyNs + "CustomerId")?.Value 
                                  ?? q.Descendants().FirstOrDefault(e => e.Name.LocalName == "CustomerId")?.Value 
                                  ?? "0";
                
                var premiumStr = q.Element(legacyNs + "Premium")?.Value 
                              ?? q.Descendants().FirstOrDefault(e => e.Name.LocalName == "Premium")?.Value 
                              ?? "0";
                
                var validUntilStr = q.Element(legacyNs + "ValidUntil")?.Value 
                                 ?? q.Descendants().FirstOrDefault(e => e.Name.LocalName == "ValidUntil")?.Value 
                                 ?? DateTime.UtcNow.ToString();
                
                var status = q.Element(legacyNs + "Status")?.Value 
                          ?? q.Descendants().FirstOrDefault(e => e.Name.LocalName == "Status")?.Value 
                          ?? string.Empty;
                
                return new QuoteResponse
                {
                    QuoteNumber = quoteNumber,
                    CustomerId = int.Parse(customerIdStr),
                    Premium = decimal.Parse(premiumStr),
                    ValidUntil = DateTime.Parse(validUntilStr),
                    Status = status
                };
            }).ToList();
            
            _logger.LogInformation("Successfully parsed {Count} quotes from SOAP response", quoteList.Count);
            
            return quoteList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing GetQuotesByCustomerResponse. XML: {Xml}", xml);
            throw;
        }
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

