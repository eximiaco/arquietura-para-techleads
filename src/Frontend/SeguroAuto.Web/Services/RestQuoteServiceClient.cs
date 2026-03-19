using System.Diagnostics;
using System.Net.Http.Json;

namespace SeguroAuto.Web.Services;

/// <summary>
/// Client REST que consome o Modern.Api em vez do SOAP legado.
/// Mesma interface IQuoteServiceClient — o controller não sabe a diferença.
/// </summary>
public class RestQuoteServiceClient : IQuoteServiceClient
{
    private static readonly ActivitySource RestActivitySource = new("SeguroAuto.Web.RestClient");
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RestQuoteServiceClient> _logger;
    private readonly string _gatewayUrl;

    public RestQuoteServiceClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RestQuoteServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        _gatewayUrl = _configuration["services__gateway__http__0"]
                   ?? Environment.GetEnvironmentVariable("services__gateway__http__0")
                   ?? "http://localhost:15100";

        _logger.LogInformation("[REST Client] initialized with Gateway URL: {GatewayUrl}", _gatewayUrl);
    }

    public async Task<QuoteResponse> GetQuoteAsync(int customerId, string vehiclePlate, string vehicleModel, int vehicleYear, bool simulateError = false)
    {
        using var activity = RestActivitySource.StartActivity("REST CreateQuote", ActivityKind.Client);
        activity?.SetTag("rpc.system", "rest");
        activity?.SetTag("rpc.method", "POST /api/quotes");
        activity?.SetTag("server.address", _gatewayUrl);

        var client = _httpClientFactory.CreateClient();
        var url = $"{_gatewayUrl.TrimEnd('/')}/api/quotes";

        _logger.LogInformation("[REST Client] POST {Url} for CustomerId: {CustomerId}", url, customerId);

        var payload = new
        {
            customerId,
            vehiclePlate,
            vehicleModel,
            vehicleYear
        };

        var response = await client.PostAsJsonAsync(url, payload);
        activity?.SetTag("http.response.status_code", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            activity?.SetStatus(ActivityStatusCode.Error, $"HTTP {(int)response.StatusCode}");
            throw new InvalidOperationException($"REST API error ({response.StatusCode}): {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<RestQuoteResponse>();

        return new QuoteResponse
        {
            QuoteNumber = result?.QuoteNumber ?? "",
            CustomerId = result?.CustomerId ?? customerId,
            Premium = result?.Premium ?? 0,
            ValidUntil = result?.ValidUntil ?? DateTime.UtcNow,
            Status = result?.Status ?? ""
        };
    }

    public async Task<List<QuoteResponse>> GetQuotesByCustomerAsync(int customerId)
    {
        using var activity = RestActivitySource.StartActivity("REST GetQuotesByCustomer", ActivityKind.Client);
        activity?.SetTag("rpc.system", "rest");
        activity?.SetTag("rpc.method", "GET /api/quotes/customer/{id}");
        activity?.SetTag("server.address", _gatewayUrl);

        var client = _httpClientFactory.CreateClient();
        var url = $"{_gatewayUrl.TrimEnd('/')}/api/quotes/customer/{customerId}";

        _logger.LogInformation("[REST Client] GET {Url}", url);

        var response = await client.GetAsync(url);
        activity?.SetTag("http.response.status_code", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            activity?.SetStatus(ActivityStatusCode.Error, $"HTTP {(int)response.StatusCode}");
            throw new InvalidOperationException($"REST API error ({response.StatusCode}): {error}");
        }

        var results = await response.Content.ReadFromJsonAsync<List<RestQuoteResponse>>() ?? new();

        return results.Select(r => new QuoteResponse
        {
            QuoteNumber = r.QuoteNumber,
            CustomerId = r.CustomerId,
            Premium = r.Premium,
            ValidUntil = r.ValidUntil,
            Status = r.Status
        }).ToList();
    }

    public async Task<bool> ApproveQuoteAsync(string quoteNumber, bool simulateError = false)
    {
        // Aprovação ainda não existe no Modern.Api — delega para SOAP
        // Isso demonstra migração incremental: nem tudo precisa migrar de uma vez
        _logger.LogWarning("[REST Client] ApproveQuote not available in REST — operation not supported in modern API yet");
        throw new InvalidOperationException(
            "Aprovação ainda não disponível na API REST moderna. " +
            "Use o modo BLUE (Legacy SOAP) para aprovar cotações.");
    }

    private class RestQuoteResponse
    {
        public string QuoteNumber { get; set; } = "";
        public int CustomerId { get; set; }
        public string VehiclePlate { get; set; } = "";
        public string VehicleModel { get; set; } = "";
        public int VehicleYear { get; set; }
        public decimal Premium { get; set; }
        public string Status { get; set; } = "";
        public DateTime ValidUntil { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
