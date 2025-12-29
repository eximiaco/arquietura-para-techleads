using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Legacy.PolicyService;
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
app.UseFaultInjection();

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder
        .AddService<PolicyService>()
        .AddServiceEndpoint<PolicyService, IPolicyService>(
            new BasicHttpBinding(),
            "/PolicyService.svc");
});

app.Run();

