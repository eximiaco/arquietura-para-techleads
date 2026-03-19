using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SeguroAuto.Web.Controllers;

/// <summary>
/// Endpoint que recebe spans do browser (OpenTelemetry client-side)
/// e os re-emite como Activities para o OTLP exporter do servidor.
/// Aceita qualquer Content-Type (sendBeacon envia como text/plain).
/// </summary>
[Route("telemetry")]
public class TelemetryController : Controller
{
    private static readonly ActivitySource BrowserActivitySource = new("SeguroAuto.Browser");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpPost("spans")]
    public async Task<IActionResult> ReceiveSpan()
    {
        // Lê o body cru — sendBeacon envia como text/plain, não application/json
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
            return Ok();

        BrowserSpan? span;
        try
        {
            span = JsonSerializer.Deserialize<BrowserSpan>(body, JsonOptions);
        }
        catch
        {
            return Ok(); // Ignora payloads inválidos silenciosamente
        }

        if (span == null || string.IsNullOrEmpty(span.TraceId) || string.IsNullOrEmpty(span.Name))
            return Ok();

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
