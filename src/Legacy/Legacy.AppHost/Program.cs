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

// PostgreSQL gerenciado pelo Aspire - sobe container automaticamente
// Senha fixa para evitar conflito com volume persistido entre execuções
var postgresPassword = builder.AddParameter("postgres-password", secret: true, value: "workshop2026");
var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithDataVolume();
var legacyDb = postgres.AddDatabase("legacydb");

// Serviços Legacy
// IMPORTANTE: Usar WithHttpEndpoint() SEM especificar porta força o Aspire
// a atribuir portas dinâmicas automaticamente para cada serviço
var quoteService = builder.AddProject<Projects.Legacy_QuoteService>("quote-service")
    .WithHttpEndpoint()
    .WithReference(legacyDb)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

var policyService = builder.AddProject<Projects.Legacy_PolicyService>("policy-service")
    .WithHttpEndpoint()
    .WithReference(legacyDb)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

var claimsService = builder.AddProject<Projects.Legacy_ClaimsService>("claims-service")
    .WithHttpEndpoint()
    .WithReference(legacyDb)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

var pricingRulesService = builder.AddProject<Projects.Legacy_PricingRulesService>("pricing-rules-service")
    .WithHttpEndpoint()
    .WithReference(legacyDb)
    .WithEnvironment("DATASET_SEED", builder.Configuration["DATASET_SEED"] ?? "1001")
    .WithEnvironment("DATASET_PROFILE", builder.Configuration["DATASET_PROFILE"] ?? "legacy")
    .WithEnvironment("FAULT_MODE", builder.Configuration["FAULT_MODE"] ?? "delay")
    .WithEnvironment("FAULT_DELAY_MS", builder.Configuration["FAULT_DELAY_MS"] ?? "300");

// Modern.Api (REST) — Strangler Fig: substitui gradualmente os endpoints SOAP
var modernApi = builder.AddProject<Projects.Modern_Api>("modern-api")
    .WithHttpEndpoint()
    .WithReference(legacyDb);

// CDC Worker — escuta mudanças no banco via PostgreSQL LISTEN/NOTIFY
var cdcWorker = builder.AddProject<Projects.Modern_CdcWorker>("cdc-worker")
    .WithReference(legacyDb);

// Worker de telemetria de banco - lê db_operation_logs e exporta spans via OTLP
var dbTelemetryWorker = builder.AddProject<Projects.Legacy_DbTelemetryWorker>("db-telemetry-worker")
    .WithReference(legacyDb);

// Frontend MVC - consome os serviços Legacy através do Gateway
var frontend = builder.AddProject<Projects.SeguroAuto_Web>("frontend")
    .WithHttpEndpoint();

// Gateway Legacy (YARP) - porta fixa 15100
// Usa service discovery do Aspire para descobrir serviços automaticamente
var gateway = builder.AddProject<Projects.Legacy_Gateway>("gateway")
    .WithHttpEndpoint(port: 15100)
    .WithReference(quoteService)
    .WithReference(policyService)
    .WithReference(claimsService)
    .WithReference(pricingRulesService)
    .WithReference(modernApi)
    .WithReference(frontend);

// Injeta URL do gateway no frontend para os SOAP clients
frontend.WithReference(gateway);

builder.Build().Run();