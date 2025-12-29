using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configurar YARP dinamicamente usando variáveis de ambiente do Aspire
// O Aspire injeta variáveis no formato: {service-name}__{endpoint-name}__{property}
// Por exemplo: quote-service__http__url
// Função auxiliar para obter URL do serviço com fallback e logging
string GetServiceUrl(string serviceName, string endpointName = "http")
{
    var envVarName = $"{serviceName}__{endpointName}__url";
    var url = Environment.GetEnvironmentVariable(envVarName);
    
    if (string.IsNullOrEmpty(url))
    {
        // Log todas as variáveis de ambiente relacionadas para debug
        var relatedVars = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(e => e.Key?.ToString()?.Contains(serviceName, StringComparison.OrdinalIgnoreCase) ?? false)
            .Select(e => $"{e.Key}={e.Value}")
            .ToList();
        
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
builder.Services.AddReverseProxy()
    .LoadFromMemory(CreateRoutes(), CreateClusters(quoteServiceUrl, policyServiceUrl, claimsServiceUrl, pricingRulesServiceUrl));

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

