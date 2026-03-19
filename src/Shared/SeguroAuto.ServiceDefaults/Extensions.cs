using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace SeguroAuto.ServiceDefaults;

public static class Extensions
{
    /// <summary>
    /// Configura OpenTelemetry (tracing + metrics) com exportação OTLP para o Aspire Dashboard.
    /// O Aspire injeta automaticamente nos processos filhos:
    ///   - OTEL_EXPORTER_OTLP_ENDPOINT (endpoint OTLP do dashboard)
    ///   - OTEL_SERVICE_NAME (nome do recurso no Aspire, ex: "quote-service")
    ///   - OTEL_RESOURCE_ATTRIBUTES (atributos adicionais do recurso)
    /// NÃO usar AddService() explicitamente para não sobrescrever esses valores.
    /// </summary>
    public static IServiceCollection AddServiceDefaults(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks();

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource("SeguroAuto.Web.SoapClient")
                    .AddSource("SeguroAuto.FaultInjection")
                    .AddSource("SeguroAuto.Database")
                    .AddSource("SeguroAuto.Browser")
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .UseOtlpExporter();

        return builder.Services;
    }

    /// <summary>
    /// Mapeia endpoints padrão de health check.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        return app;
    }
}
