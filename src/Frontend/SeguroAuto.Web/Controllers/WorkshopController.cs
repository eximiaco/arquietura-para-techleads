using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SeguroAuto.Web.Controllers;

/// <summary>
/// Painel de controle do workshop — permite demonstrar e alternar entre
/// os padrões de estrangulamento de legado em tempo real.
/// </summary>
public class WorkshopController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WorkshopController> _logger;

    public WorkshopController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WorkshopController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var gatewayUrl = _configuration["services__gateway__http__0"]
                      ?? Environment.GetEnvironmentVariable("services__gateway__http__0")
                      ?? "http://localhost:15100";

        // Consulta estado atual do gateway
        string routingMode = "blue";
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{gatewayUrl}/gateway/status");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var status = JsonDocument.Parse(json);
                routingMode = status.RootElement.GetProperty("routingMode").GetString() ?? "blue";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch gateway status");
        }

        ViewBag.RoutingMode = routingMode;
        ViewBag.GatewayUrl = gatewayUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchRouting(string mode)
    {
        var gatewayUrl = _configuration["services__gateway__http__0"]
                      ?? Environment.GetEnvironmentVariable("services__gateway__http__0")
                      ?? "http://localhost:15100";

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync($"{gatewayUrl}/gateway/routing/{mode}", null);
            var result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = mode == "green"
                    ? "Blue/Green: Cotações agora roteadas para Modern.Api REST"
                    : "Blue/Green: Cotações agora roteadas para Legacy SOAP";
            }
            else
            {
                TempData["Error"] = $"Erro ao alternar routing: {result}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching routing to {Mode}", mode);
            TempData["Error"] = $"Erro ao alternar routing: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
