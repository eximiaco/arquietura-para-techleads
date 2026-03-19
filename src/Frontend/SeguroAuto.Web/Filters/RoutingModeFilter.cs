using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SeguroAuto.Web.Filters;

/// <summary>
/// Filter global que consulta o modo Blue/Green do gateway e injeta no ViewBag.
/// Permite que o _Layout.cshtml exiba o banner indicando o modo atual.
/// </summary>
public class RoutingModeFilter : IAsyncActionFilter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    // Cache por 5s para não consultar o gateway a cada request
    private static string _cachedMode = "blue";
    private static DateTime _cacheExpiry = DateTime.MinValue;

    public RoutingModeFilter(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (DateTime.UtcNow > _cacheExpiry)
        {
            try
            {
                var gatewayUrl = _configuration["services__gateway__http__0"]
                              ?? Environment.GetEnvironmentVariable("services__gateway__http__0")
                              ?? "http://localhost:15100";

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = await client.GetAsync($"{gatewayUrl}/gateway/status");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    _cachedMode = doc.RootElement.GetProperty("routingMode").GetString() ?? "blue";
                }
            }
            catch
            {
                // Silently fallback to cached value
            }

            _cacheExpiry = DateTime.UtcNow.AddSeconds(5);
        }

        if (context.Controller is Controller controller)
        {
            controller.ViewBag.RoutingMode = _cachedMode;
        }

        await next();
    }
}
