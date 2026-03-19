using System.Text.Json;
using OpenTelemetry.Trace;
using SeguroAuto.ServiceDefaults;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
{
    tracing.AddAspNetCoreInstrumentation(options =>
    {
        options.EnrichWithHttpRequest = (activity, request) =>
        {
            activity.DisplayName = $"{request.Method} {request.Path}";
        };
    });
});

// Service discovery
string GetServiceUrl(string serviceName, string endpointName = "http")
{
    var envVarName = $"services__{serviceName}__{endpointName}__0";
    var url = Environment.GetEnvironmentVariable(envVarName);

    if (string.IsNullOrEmpty(url))
    {
        var allEnvVars = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => e.Key?.ToString() ?? "", e => e.Value?.ToString() ?? "");

        var possibleUrl = allEnvVars
            .Where(e => e.Key.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
            .Select(e => $"{e.Key}={e.Value}")
            .FirstOrDefault(v => v.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                                 v.Contains("https://", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(possibleUrl))
        {
            var urlValue = possibleUrl.Split('=', 2).LastOrDefault();
            if (!string.IsNullOrEmpty(urlValue))
            {
                Console.WriteLine($"[Gateway] Discovered {serviceName} at: {urlValue}");
                return urlValue;
            }
        }

        throw new InvalidOperationException(
            $"Service '{serviceName}' not found via service discovery.");
    }

    Console.WriteLine($"[Gateway] Discovered {serviceName} at: {url}");
    return url;
}

var quoteServiceUrl = GetServiceUrl("quote-service");
var policyServiceUrl = GetServiceUrl("policy-service");
var claimsServiceUrl = GetServiceUrl("claims-service");
var pricingRulesServiceUrl = GetServiceUrl("pricing-rules-service");
var modernApiUrl = GetServiceUrl("modern-api");
var pricingServiceUrl = GetServiceUrl("pricing-service-modern");
var frontendUrl = GetServiceUrl("frontend");

// Estado do gateway — Blue/Green alterável em runtime sem reiniciar
var routingMode = Environment.GetEnvironmentVariable("ROUTING_MODE") ?? "blue";
Console.WriteLine($"[Gateway] Initial routing mode: {routingMode}");

// YARP com InMemoryConfigProvider — permite alterar rotas em runtime
var inMemoryConfig = new Legacy.Gateway.InMemoryConfigProvider(
    BuildRoutes(routingMode),
    BuildClusters());

builder.Services.AddSingleton<IProxyConfigProvider>(inMemoryConfig);
builder.Services.AddSingleton(inMemoryConfig);
builder.Services.AddReverseProxy();

var app = builder.Build();

app.UseRouting();

// Endpoint para consultar estado do gateway
app.MapGet("/gateway/status", () => Results.Ok(new
{
    routingMode,
    description = routingMode == "green"
        ? "Cotações roteadas para Modern.Api REST"
        : "Cotações roteadas para Legacy QuoteService SOAP",
    routes = new
    {
        quotes_soap = routingMode == "green" ? "modern-api" : "quote-service",
        api = "modern-api",
        pricing = "pricing-service-modern",
        policies_soap = "policy-service",
        claims_soap = "claims-service",
        pricing_rules_soap = "pricing-rules-service"
    }
}));

// Endpoint para alternar Blue/Green em runtime
app.MapPost("/gateway/routing/{mode}", (string mode) =>
{
    if (mode != "blue" && mode != "green")
        return Results.BadRequest(new { error = "Mode must be 'blue' or 'green'" });

    routingMode = mode;
    inMemoryConfig.Update(BuildRoutes(routingMode), BuildClusters());

    Console.WriteLine($"[Gateway] Routing mode changed to: {routingMode}");
    return Results.Ok(new
    {
        routingMode,
        message = $"Routing switched to {routingMode}",
        quotes_target = routingMode == "green" ? "Modern.Api REST" : "Legacy QuoteService SOAP"
    });
});

app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();

// --- Builders de configuração YARP ---

IReadOnlyList<RouteConfig> BuildRoutes(string mode)
{
    var quoteCluster = mode == "green" ? "modern-api-cluster" : "quote-service-cluster";

    return new[]
    {
        new RouteConfig { RouteId = "modern-api", ClusterId = "modern-api-cluster", Order = 0,
            Match = new RouteMatch { Path = "/api/{**remainder}" } },
        new RouteConfig { RouteId = "pricing-service", ClusterId = "pricing-service-cluster", Order = 0,
            Match = new RouteMatch { Path = "/api/pricing/{**remainder}" } },
        new RouteConfig { RouteId = "quote-service", ClusterId = quoteCluster, Order = 1,
            Match = new RouteMatch { Path = "/QuoteService.svc/{**remainder}" } },
        new RouteConfig { RouteId = "policy-service", ClusterId = "policy-service-cluster", Order = 1,
            Match = new RouteMatch { Path = "/PolicyService.svc/{**remainder}" } },
        new RouteConfig { RouteId = "claims-service", ClusterId = "claims-service-cluster", Order = 1,
            Match = new RouteMatch { Path = "/ClaimsService.svc/{**remainder}" } },
        new RouteConfig { RouteId = "pricing-rules-service", ClusterId = "pricing-rules-service-cluster", Order = 1,
            Match = new RouteMatch { Path = "/PricingRulesService.svc/{**remainder}" } },
        new RouteConfig { RouteId = "frontend", ClusterId = "frontend-cluster", Order = 100,
            Match = new RouteMatch { Path = "/{**remainder}" } }
    };
}

IReadOnlyList<ClusterConfig> BuildClusters()
{
    ClusterConfig Cluster(string id, string address) => new()
    {
        ClusterId = id,
        Destinations = new Dictionary<string, DestinationConfig>
        {
            ["destination1"] = new() { Address = address }
        }
    };

    return new[]
    {
        Cluster("modern-api-cluster", modernApiUrl),
        Cluster("pricing-service-cluster", pricingServiceUrl),
        Cluster("quote-service-cluster", quoteServiceUrl),
        Cluster("policy-service-cluster", policyServiceUrl),
        Cluster("claims-service-cluster", claimsServiceUrl),
        Cluster("pricing-rules-service-cluster", pricingRulesServiceUrl),
        Cluster("frontend-cluster", frontendUrl)
    };
}
