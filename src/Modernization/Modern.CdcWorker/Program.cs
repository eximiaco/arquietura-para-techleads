using Modern.CdcWorker;
using OpenTelemetry;
using OpenTelemetry.Trace;
using SeguroAuto.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSeguroAutoData(builder.Configuration);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("SeguroAuto.CDC");
    })
    .UseOtlpExporter();

builder.Services.AddHostedService<CdcBackgroundService>();

var host = builder.Build();

// Cria triggers de CDC no PostgreSQL (idempotente)
await CdcSetup.EnsureTriggersAsync(host.Services);

host.Run();
