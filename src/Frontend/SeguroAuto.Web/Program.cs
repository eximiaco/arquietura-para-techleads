using SeguroAuto.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar IConfiguration para ler variáveis de ambiente
// O Aspire injeta variáveis de ambiente que precisam ser lidas
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register HttpClientFactory
builder.Services.AddHttpClient();

// Register SOAP service clients as Scoped
// Os clientes usam IHttpClientFactory, então registramos manualmente
builder.Services.AddScoped<IQuoteServiceClient, QuoteServiceClient>();
builder.Services.AddScoped<IPolicyServiceClient, PolicyServiceClient>();
builder.Services.AddScoped<IClaimsServiceClient, ClaimsServiceClient>();
builder.Services.AddScoped<IPricingRulesServiceClient, PricingRulesServiceClient>();

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

