using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Legacy.QuoteService;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;
using SeguroAuto.FaultInjection;

var builder = WebApplication.CreateBuilder(args);

// Configuração de dados
builder.Services.AddSeguroAutoData(builder.Configuration);

// Fault Injection
builder.Services.AddFaultInjection(builder.Configuration);

// CoreWCF
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

var app = builder.Build();

// Seeding do banco
await app.Services.SeedDatabaseAsync();

// Ordem correta dos middlewares:
// 1. UseRouting - necessário para o roteamento funcionar corretamente
app.UseRouting();

// 2. Fault Injection Middleware (antes do CoreWCF)
app.UseFaultInjection();

// 3. CoreWCF - configura os endpoints SOAP
app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder
        .AddService<QuoteService>()
        .AddServiceEndpoint<QuoteService, IQuoteService>(
            new BasicHttpBinding(),
            "/QuoteService.svc");
});

app.Run();

