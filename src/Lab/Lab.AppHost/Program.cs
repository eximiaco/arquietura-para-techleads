// Permitir transporte não seguro (HTTP) para desenvolvimento local
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT")))
{
    Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
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

