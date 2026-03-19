using System.Text.Json;

namespace SeguroAuto.Web.Services;

/// <summary>
/// Client que alterna entre SOAP (legacy) e REST (modern) baseado no modo
/// Blue/Green do gateway. Consulta /gateway/status para decidir qual usar.
/// O controller não sabe a diferença — mesma interface IQuoteServiceClient.
/// </summary>
public class RoutingQuoteServiceClient : IQuoteServiceClient
{
    private readonly QuoteServiceClient _soapClient;
    private readonly RestQuoteServiceClient _restClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RoutingQuoteServiceClient> _logger;

    public RoutingQuoteServiceClient(
        QuoteServiceClient soapClient,
        RestQuoteServiceClient restClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RoutingQuoteServiceClient> logger)
    {
        _soapClient = soapClient;
        _restClient = restClient;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<QuoteResponse> GetQuoteAsync(int customerId, string vehiclePlate, string vehicleModel, int vehicleYear, bool simulateError = false)
    {
        var mode = await GetRoutingModeAsync();
        _logger.LogInformation("[Routing] GetQuote via {Mode}", mode);

        if (mode == "green")
            return await _restClient.GetQuoteAsync(customerId, vehiclePlate, vehicleModel, vehicleYear, simulateError);

        return await _soapClient.GetQuoteAsync(customerId, vehiclePlate, vehicleModel, vehicleYear, simulateError);
    }

    public async Task<List<QuoteResponse>> GetQuotesByCustomerAsync(int customerId)
    {
        var mode = await GetRoutingModeAsync();
        _logger.LogInformation("[Routing] GetQuotesByCustomer via {Mode}", mode);

        if (mode == "green")
            return await _restClient.GetQuotesByCustomerAsync(customerId);

        return await _soapClient.GetQuotesByCustomerAsync(customerId);
    }

    public async Task<bool> ApproveQuoteAsync(string quoteNumber, bool simulateError = false)
    {
        // Aprovação sempre via SOAP — ainda não migrada para REST
        _logger.LogInformation("[Routing] ApproveQuote via SOAP (not yet available in REST)");
        return await _soapClient.ApproveQuoteAsync(quoteNumber, simulateError);
    }

    private async Task<string> GetRoutingModeAsync()
    {
        try
        {
            var gatewayUrl = _configuration["services__gateway__http__0"]
                          ?? Environment.GetEnvironmentVariable("services__gateway__http__0")
                          ?? "http://localhost:15100";

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{gatewayUrl}/gateway/status");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("routingMode").GetString() ?? "blue";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Routing] Could not fetch gateway status, defaulting to blue");
        }

        return "blue";
    }
}
