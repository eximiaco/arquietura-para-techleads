// Configurar variáveis do dashboard APENAS para o processo do AppHost
// ANTES de criar o builder, para que o Aspire possa detectá-las
// Usar EnvironmentVariableTarget.Process garante que sejam apenas para este processo
// e não serão herdadas pelos serviços filhos iniciados pelo Aspire
// Portas do dashboard para o demo Lab (15020-15021)
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:15020", EnvironmentVariableTarget.Process);
}

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL")))
{
    Environment.SetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:15021", EnvironmentVariableTarget.Process);
}

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT")))
{
    Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true", EnvironmentVariableTarget.Process);
}

var builder = DistributedApplication.CreateBuilder(args);

// Banco de dados SQLite - caminho absoluto baseado no diretório do projeto
var defaultDbPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "..", "..", "..", "..", "data", "lab.db"
);
var dbPath = Path.GetFullPath(builder.Configuration["DB_PATH"] ?? defaultDbPath);

// Garante que o diretório existe
var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

// Modern API
// IMPORTANTE: Usar WithHttpEndpoint() SEM especificar porta força o Aspire
// a atribuir portas dinâmicas automaticamente para cada serviço
var modernApi = builder.AddProject<Projects.Modern_Api>("modern-api")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "3003")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "lab");

// Legacy Services
var quoteService = builder.AddProject<Projects.Legacy_QuoteService>("legacy-quote-service")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "3003")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "lab")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "chaos")
    .WithEnvironment("FAULT_ERROR_RATE", builder.Configuration["FAULT_ERROR_RATE"] ?? "0.1");

var policyService = builder.AddProject<Projects.Legacy_PolicyService>("legacy-policy-service")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "3003")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "lab")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "chaos")
    .WithEnvironment("FAULT_ERROR_RATE", builder.Configuration["FAULT_ERROR_RATE"] ?? "0.1");

// Gateway com feature flags
var gateway = builder.AddProject<Projects.Lab_Gateway>("gateway")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithReference(modernApi)
    .WithReference(quoteService)
    .WithReference(policyService)
    .WithEnvironment("USE_MODERN_API", builder.Configuration["USE_MODERN_API"] ?? "false");

builder.Build().Run();