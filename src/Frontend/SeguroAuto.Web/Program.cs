using OpenTelemetry.Trace;
using SeguroAuto.ServiceDefaults;
using SeguroAuto.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar IConfiguration para ler variáveis de ambiente
// O Aspire injeta variáveis de ambiente que precisam ser lidas
builder.Configuration.AddEnvironmentVariables();

// OpenTelemetry: tracing distribuído + metrics exportados via OTLP para o Aspire Dashboard
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<SeguroAuto.Web.Filters.RoutingModeFilter>();
});

// Register HttpClientFactory
builder.Services.AddHttpClient();

// Service clients — QuoteService usa RoutingClient que alterna SOAP/REST via Blue/Green
builder.Services.AddScoped<QuoteServiceClient>();
builder.Services.AddScoped<RestQuoteServiceClient>();
builder.Services.AddScoped<IQuoteServiceClient, RoutingQuoteServiceClient>();
builder.Services.AddScoped<IPolicyServiceClient, PolicyServiceClient>();
builder.Services.AddScoped<IClaimsServiceClient, ClaimsServiceClient>();
builder.Services.AddScoped<IPricingRulesServiceClient, PricingRulesServiceClient>();

// Registra ActivitySource do REST client
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
{
    tracing.AddSource("SeguroAuto.Web.RestClient");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// HTTPS redirection desabilitado para funcionar com Aspire (usa HTTP localmente)
// app.UseHttpsRedirection();

// IMPORTANTE: UseStaticFiles deve vir ANTES de UseRouting
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers(); // API controllers com attribute routing (ex: TelemetryController)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapDefaultEndpoints();

app.Run();

