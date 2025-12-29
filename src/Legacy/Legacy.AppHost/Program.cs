// Configurar variáveis do dashboard APENAS para o processo do AppHost
// ANTES de criar o builder, para que o Aspire possa detectá-las
// Usar EnvironmentVariableTarget.Process garante que sejam apenas para este processo
// e não serão herdadas pelos serviços filhos iniciados pelo Aspire
// Portas do dashboard para o demo Legacy (15000-15001)
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:15000", EnvironmentVariableTarget.Process);
}

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL")))
{
    Environment.SetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:15001", EnvironmentVariableTarget.Process);
}

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT")))
{
    Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true", EnvironmentVariableTarget.Process);
}

var builder = DistributedApplication.CreateBuilder(args);

// Banco de dados SQLite - caminho absoluto baseado no diretório do projeto
var defaultDbPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "..", "..", "..", "..", "data", "legacy.db"
);
var dbPath = Path.GetFullPath(builder.Configuration["DB_PATH"] ?? defaultDbPath);

// Garante que o diretório existe
var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

// Serviços Legacy
// IMPORTANTE: Usar WithHttpEndpoint() SEM especificar porta força o Aspire
// a atribuir portas dinâmicas automaticamente para cada serviço
var quoteService = builder.AddProject<Projects.Legacy_QuoteService>("quote-service")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

var policyService = builder.AddProject<Projects.Legacy_PolicyService>("policy-service")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

var claimsService = builder.AddProject<Projects.Legacy_ClaimsService>("claims-service")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

var pricingRulesService = builder.AddProject<Projects.Legacy_PricingRulesService>("pricing-rules-service")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

// Frontend MVC - consome os serviços Legacy através do Gateway
// DEVE ser definido ANTES do Gateway para poder ser referenciado nas rotas
var frontend = builder.AddProject<Projects.SeguroAuto_Web>("frontend")
    .WithHttpEndpoint();
    
// Nota: A referência ao gateway será adicionada depois que o gateway for criado

// Gateway Legacy usando AddYarp() nativo do Aspire
// Expõe todos os serviços Legacy e o Frontend através de uma única porta
// Usa service discovery automático do Aspire - sem problemas de HttpSys!
// Nota: AddYarp() já cria o endpoint HTTP automaticamente, não precisa chamar WithHttpEndpoint()
var gateway = builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        // Rotas para serviços SOAP (devem vir antes da rota catch-all do frontend)
        yarp.AddRoute("/QuoteService.svc/{**catch-all}", quoteService);
        yarp.AddRoute("/PolicyService.svc/{**catch-all}", policyService);
        yarp.AddRoute("/ClaimsService.svc/{**catch-all}", claimsService);
        yarp.AddRoute("/PricingRulesService.svc/{**catch-all}", pricingRulesService);
        
        // Rota para o frontend - captura todas as outras requisições
        // IMPORTANTE: Esta rota deve ser a última para não interceptar as rotas SOAP
        yarp.AddRoute("/{**catch-all}", frontend);
    });

// Adiciona referência do frontend ao gateway para service discovery
// Isso garante que a variável services__gateway__http__0 seja injetada no frontend
frontend.WithReference(gateway);

builder.Build().Run();