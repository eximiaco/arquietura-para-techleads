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

// Gateway Legacy - expõe todos os serviços Legacy através de uma única porta
// Usa service discovery do Aspire para descobrir as URLs dos serviços dinamicamente
var gateway = builder.AddProject<Projects.Legacy_Gateway>("gateway")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithReference(quoteService)
    .WithReference(policyService)
    .WithReference(claimsService)
    .WithReference(pricingRulesService);

builder.Build().Run();