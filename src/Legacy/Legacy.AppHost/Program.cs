// Configurar variáveis de ambiente do dashboard antes de criar o builder
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:15000");
}

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL")))
{
    Environment.SetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:15001");
}

// Permitir transporte não seguro (HTTP) para desenvolvimento local
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT")))
{
    Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
}

var builder = DistributedApplication.CreateBuilder(args);

// Banco de dados SQLite - caminho local
var dbPath = builder.Configuration["DB_PATH"] ?? "./data/legacy.db";

// Serviços Legacy
var quoteService = builder.AddProject<Projects.Legacy_QuoteService>("quote-service")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

var policyService = builder.AddProject<Projects.Legacy_PolicyService>("policy-service")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

var claimsService = builder.AddProject<Projects.Legacy_ClaimsService>("claims-service")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

var pricingRulesService = builder.AddProject<Projects.Legacy_PricingRulesService>("pricing-rules-service")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

builder.Build().Run();

