using Modern.Api.AntiCorruption;
using OpenTelemetry.Trace;
using SeguroAuto.Data;
using SeguroAuto.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSeguroAutoData(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddScoped<LegacyQuoteAdapter>();
builder.Services.AddControllers();

// Registra ActivitySource do ACL
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
{
    tracing.AddSource("SeguroAuto.ACL");
});

var app = builder.Build();

await app.Services.SeedDatabaseAsync();

app.UseRouting();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
