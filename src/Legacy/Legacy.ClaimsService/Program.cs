using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Legacy.ClaimsService;
using SeguroAuto.Data;
using SeguroAuto.FaultInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSeguroAutoData(builder.Configuration);
builder.Services.AddFaultInjection(builder.Configuration);
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

var app = builder.Build();

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
        .AddService<ClaimsService>()
        .AddServiceEndpoint<ClaimsService, IClaimsService>(
            new BasicHttpBinding(),
            "/ClaimsService.svc");
});

app.Run();

