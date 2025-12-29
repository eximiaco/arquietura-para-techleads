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

var dbPath = builder.Configuration["DB_PATH"] ?? "./data/modernization.db";

// Modern API
var modernApi = builder.AddProject<Projects.Modern_Api>("modern-api")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "2002")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "modern");

// Legacy Services
var quoteService = builder.AddProject<Projects.Legacy_QuoteService>("legacy-quote-service")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "2002")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "modern")
    .WithEnvironment("FAULT_MODE", "off");

var policyService = builder.AddProject<Projects.Legacy_PolicyService>("legacy-policy-service")
    .WithEnvironment("DB_PATH", dbPath)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "2002")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "modern")
    .WithEnvironment("FAULT_MODE", "off");

// Gateway
var gateway = builder.AddProject<Projects.Modern_Gateway>("gateway")
    .WithReference(modernApi)
    .WithReference(quoteService)
    .WithReference(policyService);

builder.Build().Run();

