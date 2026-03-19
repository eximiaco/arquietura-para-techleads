using SeguroAuto.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry: tracing distribuído + metrics exportados via OTLP para o Aspire Dashboard
builder.AddServiceDefaults();

// Função auxiliar para obter URL do serviço via service discovery do Aspire
// O Aspire injeta variáveis no formato: services__{service-name}__{endpoint-name}__{index}
string GetServiceUrl(string serviceName, string endpointName = "http")
{
    var envVarName = $"services__{serviceName}__{endpointName}__0";
    var url = Environment.GetEnvironmentVariable(envVarName);

    if (string.IsNullOrEmpty(url))
    {
        var allEnvVars = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => e.Key?.ToString() ?? "", e => e.Value?.ToString() ?? "");

        var relatedVars = allEnvVars
            .Where(e => e.Key.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
            .Select(e => $"{e.Key}={e.Value}")
            .ToList();

        var possibleUrl = relatedVars
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
            $"Environment variable '{envVarName}' not found for service '{serviceName}'. " +
            $"Related vars: {string.Join(", ", relatedVars)}");
    }

    Console.WriteLine($"[Gateway] Discovered {serviceName} at: {url}");
    return url;
}

var quoteServiceUrl = GetServiceUrl("quote-service");
var policyServiceUrl = GetServiceUrl("policy-service");
var claimsServiceUrl = GetServiceUrl("claims-service");
var pricingRulesServiceUrl = GetServiceUrl("pricing-rules-service");
var frontendUrl = GetServiceUrl("frontend");

// Configurar YARP com rotas nomeadas para rastreabilidade no tracing
// O nome da rota (key) aparece nos spans do OpenTelemetry
var configDict = new Dictionary<string, string?>
{
    // Rotas SOAP - prioritárias (Order menor = maior prioridade)
    ["ReverseProxy:Routes:quote-service:ClusterId"] = "quote-service-cluster",
    ["ReverseProxy:Routes:quote-service:Match:Path"] = "/QuoteService.svc/{**remainder}",
    ["ReverseProxy:Routes:quote-service:Order"] = "1",

    ["ReverseProxy:Routes:policy-service:ClusterId"] = "policy-service-cluster",
    ["ReverseProxy:Routes:policy-service:Match:Path"] = "/PolicyService.svc/{**remainder}",
    ["ReverseProxy:Routes:policy-service:Order"] = "1",

    ["ReverseProxy:Routes:claims-service:ClusterId"] = "claims-service-cluster",
    ["ReverseProxy:Routes:claims-service:Match:Path"] = "/ClaimsService.svc/{**remainder}",
    ["ReverseProxy:Routes:claims-service:Order"] = "1",

    ["ReverseProxy:Routes:pricing-rules-service:ClusterId"] = "pricing-rules-service-cluster",
    ["ReverseProxy:Routes:pricing-rules-service:Match:Path"] = "/PricingRulesService.svc/{**remainder}",
    ["ReverseProxy:Routes:pricing-rules-service:Order"] = "1",

    // Rota frontend - catch-all com menor prioridade
    ["ReverseProxy:Routes:frontend:ClusterId"] = "frontend-cluster",
    ["ReverseProxy:Routes:frontend:Match:Path"] = "/{**remainder}",
    ["ReverseProxy:Routes:frontend:Order"] = "100",

    // Clusters (destinos)
    ["ReverseProxy:Clusters:quote-service-cluster:Destinations:destination1:Address"] = quoteServiceUrl,
    ["ReverseProxy:Clusters:policy-service-cluster:Destinations:destination1:Address"] = policyServiceUrl,
    ["ReverseProxy:Clusters:claims-service-cluster:Destinations:destination1:Address"] = claimsServiceUrl,
    ["ReverseProxy:Clusters:pricing-rules-service-cluster:Destinations:destination1:Address"] = pricingRulesServiceUrl,
    ["ReverseProxy:Clusters:frontend-cluster:Destinations:destination1:Address"] = frontendUrl
};

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(configDict)
    .Build();

builder.Services.AddReverseProxy()
    .LoadFromConfig(config.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseRouting();
app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();
