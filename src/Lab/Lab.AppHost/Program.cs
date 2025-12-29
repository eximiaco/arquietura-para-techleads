// Configurar variáveis de ambiente do dashboard antes de criar o builder
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://localhost:15000");
}

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL")))
{
    Environment.SetEnvironmentVariable("DOTNET_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:15001");
}

var builder = DistributedApplication.CreateBuilder(args);

var dbPath = builder.Configuration["DB_PATH"] ?? "./data/lab.db";

// Modern API
var modernApi = builder.AddProject<Projects.Modern_Api>("modern-api")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "3003")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "lab");

// Legacy Services
var quoteService = builder.AddProject<Projects.Legacy_QuoteService>("legacy-quote-service")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "3003")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "lab")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "chaos")
    .WithEnvironment("FAULT_ERROR_RATE", builder.Configuration["FAULT_ERROR_RATE"] ?? "0.1");

var policyService = builder.AddProject<Projects.Legacy_PolicyService>("legacy-policy-service")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "3003")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "lab")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "chaos")
    .WithEnvironment("FAULT_ERROR_RATE", builder.Configuration["FAULT_ERROR_RATE"] ?? "0.1");

// Gateway com feature flags
var gateway = builder.AddProject<Projects.Lab_Gateway>("gateway")
    .WithReference(modernApi)
    .WithReference(quoteService)
    .WithReference(policyService)
    .WithEnvironment("USE_MODERN_API", builder.Configuration["USE_MODERN_API"] ?? "false");

builder.Build().Run();

