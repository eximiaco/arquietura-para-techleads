using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SeguroAuto.Web.Controllers;

/// <summary>
/// Endpoint que recebe spans do browser (OpenTelemetry client-side)
/// e os re-emite como Activities para o OTLP exporter do servidor.
/// Isso permite que interações do usuário (clicks, form submits, page loads)
/// apareçam no Aspire Dashboard conectadas ao trace distribuído.
/// </summary>
[ApiController]
[Route("telemetry")]
public class TelemetryController : ControllerBase
{
    private static readonly ActivitySource BrowserActivitySource = new("SeguroAuto.Browser");

    [HttpPost("spans")]
    public IActionResult ReceiveSpan([FromBody] BrowserSpan span)
    {
        if (string.IsNullOrEmpty(span.TraceId) || string.IsNullOrEmpty(span.Name))
            return BadRequest();

        // Reconstrói o parent context para vincular ao trace distribuído
        ActivityContext parentContext = default;
        try
        {
            var traceId = ActivityTraceId.CreateFromString(span.TraceId.AsSpan());
            var parentSpanId = !string.IsNullOrEmpty(span.ParentSpanId)
                ? ActivitySpanId.CreateFromString(span.ParentSpanId.AsSpan())
                : default;
            parentContext = new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded);
        }
        catch
        {
            // Se o trace context for inválido, cria span sem parent
        }

        using var activity = BrowserActivitySource.StartActivity(
            span.Name,
            ActivityKind.Client,
            parentContext);

        if (activity != null)
        {
            if (DateTime.TryParse(span.StartedAt, out var startTime))
                activity.SetStartTime(startTime.ToUniversalTime());

            activity.SetTag("browser.source", "client");

            if (span.Attributes != null)
            {
                foreach (var (key, value) in span.Attributes)
                {
                    activity.SetTag(key, value);
                }
            }

            if (DateTime.TryParse(span.EndedAt, out var endTime))
                activity.SetEndTime(endTime.ToUniversalTime());
        }

        return Ok();
    }
}

public class BrowserSpan
{
    public string TraceId { get; set; } = "";
    public string ParentSpanId { get; set; } = "";
    public string SpanId { get; set; } = "";
    public string Name { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public string EndedAt { get; set; } = "";
    public Dictionary<string, string>? Attributes { get; set; }
}
