// Configurar variáveis do dashboard APENAS para o processo do AppHost
// ANTES de criar o builder, para que o Aspire possa detectá-las
// Usar EnvironmentVariableTarget.Process garante que sejam apenas para este processo
// e não serão herdadas pelos serviços filhos iniciados pelo Aspire
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
    "..", "..", "..", "..", "data", "modernization.db"
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
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "2002")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "modern");

// Legacy Services
var quoteService = builder.AddProject<Projects.Legacy_QuoteService>("legacy-quote-service")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "2002")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "modern")
    .WithEnvironment("FAULT_MODE", "off");

var policyService = builder.AddProject<Projects.Legacy_PolicyService>("legacy-policy-service")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "2002")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "modern")
    .WithEnvironment("FAULT_MODE", "off");

// Gateway
var gateway = builder.AddProject<Projects.Modern_Gateway>("gateway")
    .WithHttpEndpoint() // Força o Aspire a gerenciar a porta dinamicamente
    .WithReference(modernApi)
    .WithReference(quoteService)
    .WithReference(policyService);

builder.Build().Run();