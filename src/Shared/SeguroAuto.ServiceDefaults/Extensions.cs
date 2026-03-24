using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Npgsql;
using OpenTelemetry.Trace;

namespace SeguroAuto.ServiceDefaults;

public static class Extensions
{
    /// <summary>
    /// Configura OpenTelemetry (tracing + metrics) com exportação OTLP para o Aspire Dashboard.
    /// Inclui Resource Detectors para informações de container, host e processo.
    /// </summary>
    public static IServiceCollection AddServiceDefaults(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                // Informações do host e processo (aparecem em todos os spans do serviço)
                var attributes = new List<KeyValuePair<string, object>>
                {
                    new("host.name", Environment.MachineName),
                    new("os.description", RuntimeInformation.OSDescription),
                    new("os.type", OperatingSystem.IsLinux() ? "linux" :
                                   OperatingSystem.IsWindows() ? "windows" :
                                   OperatingSystem.IsMacOS() ? "darwin" : "unknown"),
                    new("process.pid", Environment.ProcessId),
                    new("process.runtime.name", RuntimeInformation.FrameworkDescription),
                    new("process.runtime.version", Environment.Version.ToString())
                };

                // Container ID — lê de /proc/self/cgroup (disponível em containers Linux)
                var containerId = GetContainerId();
                if (!string.IsNullOrEmpty(containerId))
                {
                    attributes.Add(new("container.id", containerId));
                }

                resource.AddAttributes(attributes);
            })
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
                    })
                    .AddNpgsql();
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
    /// Tenta ler o container ID de /proc/self/cgroup (Linux containers).
    /// </summary>
    private static string? GetContainerId()
    {
        if (!OperatingSystem.IsLinux()) return null;

        try
        {
            // Formato cgroup v2: "0::/docker/{containerId}"
            // Formato cgroup v1: "12:memory:/docker/{containerId}"
            var cgroupPath = "/proc/self/cgroup";
            if (!File.Exists(cgroupPath)) return null;

            foreach (var line in File.ReadLines(cgroupPath))
            {
                var parts = line.Split('/');
                if (parts.Length >= 3 && parts[^1].Length >= 12)
                {
                    return parts[^1]; // último segmento é o container ID
                }
            }
        }
        catch { }

        return null;
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
