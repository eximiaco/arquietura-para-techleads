using System.Net;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SeguroAuto.FaultInjection;

public class FaultInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FaultInjectionMiddleware> _logger;
    private readonly FaultInjectionOptions _options;

    public FaultInjectionMiddleware(
        RequestDelegate next,
        ILogger<FaultInjectionMiddleware> logger,
        FaultInjectionOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_options.Mode == FaultMode.Off)
        {
            await _next(context);
            return;
        }

        switch (_options.Mode)
        {
            case FaultMode.Delay:
                await HandleDelayAsync(context);
                break;
            case FaultMode.Error:
                await HandleErrorAsync(context);
                return; // Não continua o pipeline
            case FaultMode.Chaos:
                if (ShouldApplyChaos())
                {
                    await HandleChaosAsync(context);
                    return; // Não continua o pipeline
                }
                await HandleDelayAsync(context); // Aplica delay mesmo em modo chaos
                break;
        }

        await _next(context);
    }

    private async Task HandleDelayAsync(HttpContext context)
    {
        if (_options.DelayMs > 0)
        {
            _logger.LogWarning("Fault Injection: Applying delay of {DelayMs}ms", _options.DelayMs);
            await Task.Delay(_options.DelayMs);
        }
    }

    private async Task HandleErrorAsync(HttpContext context)
    {
        _logger.LogWarning("Fault Injection: Returning error {ErrorKind}", _options.ErrorKind);
        await ReturnErrorAsync(context, _options.ErrorKind);
    }

    private async Task HandleChaosAsync(HttpContext context)
    {
        var errorKind = GetRandomErrorKind();
        _logger.LogWarning("Fault Injection: Chaos mode - returning error {ErrorKind}", errorKind);
        await ReturnErrorAsync(context, errorKind);
    }

    private bool ShouldApplyChaos()
    {
        if (_options.ErrorRate <= 0)
            return false;

        var random = new Random();
        return random.NextDouble() < _options.ErrorRate;
    }

    private FaultErrorKind GetRandomErrorKind()
    {
        var random = new Random();
        var kinds = Enum.GetValues<FaultErrorKind>();
        return kinds[random.Next(kinds.Length)];
    }

    private async Task ReturnErrorAsync(HttpContext context, FaultErrorKind errorKind)
    {
        context.Response.StatusCode = errorKind switch
        {
            FaultErrorKind.Http503 => (int)HttpStatusCode.ServiceUnavailable,
            FaultErrorKind.Timeout => (int)HttpStatusCode.RequestTimeout,
            FaultErrorKind.SoapFault => (int)HttpStatusCode.InternalServerError,
            _ => (int)HttpStatusCode.InternalServerError
        };

        if (errorKind == FaultErrorKind.SoapFault)
        {
            context.Response.ContentType = "text/xml; charset=utf-8";
            var soapFault = GenerateSoapFault();
            await context.Response.WriteAsync(soapFault);
        }
        else
        {
            context.Response.ContentType = "application/json";
            var errorMessage = errorKind switch
            {
                FaultErrorKind.Http503 => "{\"error\":\"Service Unavailable\"}",
                FaultErrorKind.Timeout => "{\"error\":\"Request Timeout\"}",
                _ => "{\"error\":\"Internal Server Error\"}"
            };
            await context.Response.WriteAsync(errorMessage);
        }
    }

    private string GenerateSoapFault()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <soap:Fault>
      <faultcode>soap:Server</faultcode>
      <faultstring>Fault injection: Internal server error</faultstring>
      <detail>
        <message>This is a simulated SOAP fault for testing purposes.</message>
      </detail>
    </soap:Fault>
  </soap:Body>
</soap:Envelope>";
    }
}

public class FaultInjectionOptions
{
    public FaultMode Mode { get; set; } = FaultMode.Off;
    public int DelayMs { get; set; } = 0;
    public double ErrorRate { get; set; } = 0.1; // 10% por padrão
    public FaultErrorKind ErrorKind { get; set; } = FaultErrorKind.Http503;
}

public enum FaultMode
{
    Off,
    Delay,
    Error,
    Chaos
}

public enum FaultErrorKind
{
    Http503,
    Timeout,
    SoapFault
}

public static class FaultInjectionExtensions
{
    public static IServiceCollection AddFaultInjection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new FaultInjectionOptions();

        var faultMode = configuration["FAULT_MODE"] ?? "off";
        options.Mode = faultMode.ToLower() switch
        {
            "delay" => FaultMode.Delay,
            "error" => FaultMode.Error,
            "chaos" => FaultMode.Chaos,
            _ => FaultMode.Off
        };

        if (int.TryParse(configuration["FAULT_DELAY_MS"], out var delayMs))
        {
            options.DelayMs = delayMs;
        }

        if (double.TryParse(configuration["FAULT_ERROR_RATE"], out var errorRate))
        {
            options.ErrorRate = Math.Clamp(errorRate, 0.0, 1.0);
        }

        var errorKind = configuration["FAULT_ERROR_KIND"] ?? "http503";
        options.ErrorKind = errorKind.ToLower() switch
        {
            "timeout" => FaultErrorKind.Timeout,
            "soapfault" => FaultErrorKind.SoapFault,
            _ => FaultErrorKind.Http503
        };

        services.AddSingleton(options);
        return services;
    }

    public static IApplicationBuilder UseFaultInjection(this IApplicationBuilder app)
    {
        return app.UseMiddleware<FaultInjectionMiddleware>();
    }
}

