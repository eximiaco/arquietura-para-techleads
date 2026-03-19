using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace Modern.Api.AntiCorruption;

/// <summary>
/// Anti-Corruption Layer: traduz entre o modelo moderno (REST/JSON) e o legado (SOAP/XML).
/// O Modern.Api NÃO depende diretamente dos contratos WCF — este adapter isola a complexidade.
/// </summary>
public class LegacyQuoteAdapter
{
    private static readonly ActivitySource AclActivitySource = new("SeguroAuto.ACL");
    private readonly HttpClient _httpClient;
    private readonly string _legacyUrl;
    private readonly ILogger<LegacyQuoteAdapter> _logger;
    private const string Namespace = "http://eximia.co/seguroauto/legacy";

    public LegacyQuoteAdapter(HttpClient httpClient, IConfiguration configuration, ILogger<LegacyQuoteAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _legacyUrl = configuration["services__quote-service__http__0"]
                  ?? Environment.GetEnvironmentVariable("services__quote-service__http__0")
                  ?? "http://localhost:5000";
    }

    /// <summary>
    /// Traduz um request REST de criação de cotação para SOAP e envia ao legado.
    /// Retorna o resultado traduzido de volta para o modelo moderno.
    /// </summary>
    public async Task<CreateQuoteResult> CreateQuoteAsync(CreateQuoteCommand command)
    {
        using var activity = AclActivitySource.StartActivity("ACL TranslateToSOAP", ActivityKind.Client);
        activity?.SetTag("acl.direction", "modern_to_legacy");
        activity?.SetTag("acl.operation", "CreateQuote");
        activity?.SetTag("acl.legacy_protocol", "SOAP/XML");

        // Traduz modelo moderno → envelope SOAP
        var soapEnvelope = BuildSoapEnvelope(command);
        activity?.SetTag("acl.soap_envelope", soapEnvelope);

        _logger.LogInformation("[ACL] Translating CreateQuote to SOAP for CustomerId: {CustomerId}", command.CustomerId);

        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", $"{Namespace}/IQuoteService/GetQuote");

        var response = await _httpClient.PostAsync($"{_legacyUrl}/QuoteService.svc", content);
        var responseXml = await response.Content.ReadAsStringAsync();

        activity?.SetTag("acl.http_status", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            activity?.SetStatus(ActivityStatusCode.Error, $"Legacy returned {(int)response.StatusCode}");
            throw new InvalidOperationException($"Legacy QuoteService returned {response.StatusCode}: {responseXml}");
        }

        // Traduz resposta SOAP → modelo moderno
        var result = ParseSoapResponse(responseXml);
        activity?.SetTag("acl.result.quote_number", result.QuoteNumber);

        _logger.LogInformation("[ACL] Quote created via legacy: {QuoteNumber}", result.QuoteNumber);

        return result;
    }

    private string BuildSoapEnvelope(CreateQuoteCommand command)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:legacy=""{Namespace}"">
    <soap:Body>
        <legacy:QuoteRequest>
            <legacy:CustomerId>{command.CustomerId}</legacy:CustomerId>
            <legacy:VehiclePlate>{EscapeXml(command.VehiclePlate)}</legacy:VehiclePlate>
            <legacy:VehicleModel>{EscapeXml(command.VehicleModel)}</legacy:VehicleModel>
            <legacy:VehicleYear>{command.VehicleYear}</legacy:VehicleYear>
            <legacy:SimulateError>false</legacy:SimulateError>
        </legacy:QuoteRequest>
    </soap:Body>
</soap:Envelope>";
    }

    private CreateQuoteResult ParseSoapResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
        var legacyNs = XNamespace.Get(Namespace);

        var body = doc.Descendants(ns + "Body").FirstOrDefault();
        var qr = body?.Descendants().FirstOrDefault(e => e.Name.LocalName == "QuoteResponse");

        if (qr == null)
            throw new InvalidOperationException("Invalid SOAP response from legacy QuoteService");

        return new CreateQuoteResult
        {
            QuoteNumber = qr.Descendants().FirstOrDefault(e => e.Name.LocalName == "QuoteNumber")?.Value ?? "",
            CustomerId = int.Parse(qr.Descendants().FirstOrDefault(e => e.Name.LocalName == "CustomerId")?.Value ?? "0"),
            Premium = decimal.Parse(qr.Descendants().FirstOrDefault(e => e.Name.LocalName == "Premium")?.Value ?? "0"),
            Status = qr.Descendants().FirstOrDefault(e => e.Name.LocalName == "Status")?.Value ?? "",
            ValidUntil = DateTime.Parse(qr.Descendants().FirstOrDefault(e => e.Name.LocalName == "ValidUntil")?.Value ?? DateTime.UtcNow.ToString())
        };
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                   .Replace("\"", "&quot;").Replace("'", "&apos;");
    }
}

public class CreateQuoteCommand
{
    public int CustomerId { get; set; }
    public string VehiclePlate { get; set; } = "";
    public string VehicleModel { get; set; } = "";
    public int VehicleYear { get; set; }
}

public class CreateQuoteResult
{
    public string QuoteNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public decimal Premium { get; set; }
    public string Status { get; set; } = "";
    public DateTime ValidUntil { get; set; }
}
