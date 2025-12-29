using Microsoft.Extensions.Configuration;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Garantir que estamos usando apenas Kestrel (não HttpSys)
// HttpSys não está disponível no macOS/Linux
builder.WebHost.UseKestrel();

// Desabilitar HttpSys delegation no YARP para evitar TypeLoadException no macOS
// O YARP tentará usar HttpSys delegation automaticamente se detectar HttpSys,
// mas como estamos usando Kestrel, precisamos evitar essa tentativa
Environment.SetEnvironmentVariable("ASPNETCORE_SERVER_URLS", "");

// Configurar YARP dinamicamente usando variáveis de ambiente do Aspire
// O Aspire injeta variáveis no formato: services__{service-name}__{endpoint-name}__{index}
// Por exemplo: services__quote-service__http__0
// Função auxiliar para obter URL do serviço com fallback e logging
string GetServiceUrl(string serviceName, string endpointName = "http")
{
    // Tentar o formato correto do Aspire: services__{service-name}__{endpoint-name}__0
    var envVarName = $"services__{serviceName}__{endpointName}__0";
    var url = Environment.GetEnvironmentVariable(envVarName);
    
    if (string.IsNullOrEmpty(url))
    {
        // Tentar formatos alternativos ou buscar todas as variáveis relacionadas
        var allEnvVars = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => e.Key?.ToString() ?? "", e => e.Value?.ToString() ?? "");
        
        // Procurar por qualquer variável que contenha o nome do serviço
        var relatedVars = allEnvVars
            .Where(e => e.Key.Contains(serviceName, StringComparison.OrdinalIgnoreCase))
            .Select(e => $"{e.Key}={e.Value}")
            .ToList();
        
        // Tentar encontrar a URL em qualquer formato relacionado
        var possibleUrl = relatedVars
            .FirstOrDefault(v => v.Contains("http://", StringComparison.OrdinalIgnoreCase) || 
                                 v.Contains("https://", StringComparison.OrdinalIgnoreCase));
        
        if (!string.IsNullOrEmpty(possibleUrl))
        {
            // Extrair a URL do formato "key=value"
            var urlValue = possibleUrl.Split('=', 2).LastOrDefault();
            if (!string.IsNullOrEmpty(urlValue))
            {
                Console.WriteLine($"[Gateway] Discovered {serviceName} at: {urlValue} (from {possibleUrl.Split('=').First()})");
                return urlValue;
            }
        }
        
        var errorMsg = $"Environment variable '{envVarName}' not found for service '{serviceName}'. " +
                      $"Make sure the service is referenced in the AppHost using .WithReference(). " +
                      $"Related environment variables found: {string.Join(", ", relatedVars)}";
        
        throw new InvalidOperationException(errorMsg);
    }
    
    Console.WriteLine($"[Gateway] Discovered {serviceName} at: {url}");
    return url;
}

var quoteServiceUrl = GetServiceUrl("quote-service");
var policyServiceUrl = GetServiceUrl("policy-service");
var claimsServiceUrl = GetServiceUrl("claims-service");
var pricingRulesServiceUrl = GetServiceUrl("pricing-rules-service");

// Configurar YARP programaticamente
// Nota: HttpSys delegation não está disponível no macOS/Linux (apenas Windows)
// O YARP funcionará com Kestrel sem problemas
var routes = CreateRoutes();
var clusters = CreateClusters(quoteServiceUrl, policyServiceUrl, claimsServiceUrl, pricingRulesServiceUrl);

// Criar configuração em memória e converter para IConfiguration
// Isso evita problemas com HttpSys delegation no macOS
var configDict = new Dictionary<string, string?>
{
    ["ReverseProxy:Routes:quote-service:ClusterId"] = "quote-service-cluster",
    ["ReverseProxy:Routes:quote-service:Match:Path"] = "/QuoteService.svc/{**catch-all}",
    ["ReverseProxy:Routes:policy-service:ClusterId"] = "policy-service-cluster",
    ["ReverseProxy:Routes:policy-service:Match:Path"] = "/PolicyService.svc/{**catch-all}",
    ["ReverseProxy:Routes:claims-service:ClusterId"] = "claims-service-cluster",
    ["ReverseProxy:Routes:claims-service:Match:Path"] = "/ClaimsService.svc/{**catch-all}",
    ["ReverseProxy:Routes:pricing-rules-service:ClusterId"] = "pricing-rules-service-cluster",
    ["ReverseProxy:Routes:pricing-rules-service:Match:Path"] = "/PricingRulesService.svc/{**catch-all}",
    ["ReverseProxy:Clusters:quote-service-cluster:Destinations:destination1:Address"] = quoteServiceUrl,
    ["ReverseProxy:Clusters:policy-service-cluster:Destinations:destination1:Address"] = policyServiceUrl,
    ["ReverseProxy:Clusters:claims-service-cluster:Destinations:destination1:Address"] = claimsServiceUrl,
    ["ReverseProxy:Clusters:pricing-rules-service-cluster:Destinations:destination1:Address"] = pricingRulesServiceUrl
};

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(configDict)
    .Build();

builder.Services.AddReverseProxy()
    .LoadFromConfig(config.GetSection("ReverseProxy"));

var app = builder.Build();

// Ordem correta dos middlewares:
// 1. UseRouting - necessário para o roteamento funcionar corretamente
app.UseRouting();

// 2. YARP Reverse Proxy - roteia requisições para os serviços backend
app.MapReverseProxy();

app.Run();

// Configuração de rotas
static RouteConfig[] CreateRoutes()
{
    return new[]
    {
        new RouteConfig
        {
            RouteId = "quote-service",
            ClusterId = "quote-service-cluster",
            Match = new RouteMatch
            {
                Path = "/QuoteService.svc/{**catch-all}"
            }
        },
        new RouteConfig
        {
            RouteId = "policy-service",
            ClusterId = "policy-service-cluster",
            Match = new RouteMatch
            {
                Path = "/PolicyService.svc/{**catch-all}"
            }
        },
        new RouteConfig
        {
            RouteId = "claims-service",
            ClusterId = "claims-service-cluster",
            Match = new RouteMatch
            {
                Path = "/ClaimsService.svc/{**catch-all}"
            }
        },
        new RouteConfig
        {
            RouteId = "pricing-rules-service",
            ClusterId = "pricing-rules-service-cluster",
            Match = new RouteMatch
            {
                Path = "/PricingRulesService.svc/{**catch-all}"
            }
        }
    };
}

// Configuração de clusters usando URLs dinâmicas do Aspire
static ClusterConfig[] CreateClusters(string quoteUrl, string policyUrl, string claimsUrl, string pricingRulesUrl)
{
    return new[]
    {
        new ClusterConfig
        {
            ClusterId = "quote-service-cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["destination1"] = new DestinationConfig
                {
                    Address = quoteUrl
                }
            }
        },
        new ClusterConfig
        {
            ClusterId = "policy-service-cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["destination1"] = new DestinationConfig
                {
                    Address = policyUrl
                }
            }
        },
        new ClusterConfig
        {
            ClusterId = "claims-service-cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["destination1"] = new DestinationConfig
                {
                    Address = claimsUrl
                }
            }
        },
        new ClusterConfig
        {
            ClusterId = "pricing-rules-service-cluster",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["destination1"] = new DestinationConfig
                {
                    Address = pricingRulesUrl
                }
            }
        }
    };
}

