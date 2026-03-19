using Legacy.DbTelemetryWorker;
using OpenTelemetry;
using OpenTelemetry.Trace;
using SeguroAuto.Data;

var builder = Host.CreateApplicationBuilder(args);

// Banco de dados (mesmo SQLite compartilhado com os serviços Legacy)
builder.Services.AddSeguroAutoData(builder.Configuration);

// OpenTelemetry: exporta spans reconstruídos a partir da tabela db_operation_logs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("SeguroAuto.Database");
    })
    .UseOtlpExporter();

builder.Services.AddHostedService<DbTelemetryBackgroundService>();

var host = builder.Build();
host.Run();
