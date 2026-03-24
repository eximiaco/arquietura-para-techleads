using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Modern.CdcWorker;

/// <summary>
/// Worker que escuta eventos CDC do PostgreSQL via LISTEN/NOTIFY.
/// Cada evento de mudança (INSERT/UPDATE/DELETE) gera um span no tracing,
/// com ActivityLink para o trace original quando TraceId está disponível.
/// </summary>
public class CdcBackgroundService : BackgroundService
{
    private static readonly ActivitySource CdcActivitySource = new("SeguroAuto.CDC");
    private readonly IConfiguration _configuration;
    private readonly ILogger<CdcBackgroundService> _logger;

    public CdcBackgroundService(IConfiguration configuration, ILogger<CdcBackgroundService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = _configuration.GetConnectionString("legacydb");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("Connection string 'legacydb' not found. CDC worker cannot start.");
            return;
        }

        _logger.LogInformation("CDC Worker started. Listening for changes on channel 'cdc_events'...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);

                await using var cmd = new NpgsqlCommand("LISTEN cdc_events", connection);
                await cmd.ExecuteNonQueryAsync(stoppingToken);

                connection.Notification += (_, args) =>
                {
                    ProcessCdcEvent(args.Payload);
                };

                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CDC listener error. Reconnecting in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void ProcessCdcEvent(string payload)
    {
        try
        {
            var evt = JsonSerializer.Deserialize<CdcEvent>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (evt == null) return;

            _logger.LogInformation("CDC Event: {Operation} on {Table} at {Timestamp}",
                evt.Operation, evt.Table, evt.Timestamp);

            // Tenta extrair TraceId do payload para criar link ao trace original
            var links = new List<ActivityLink>();
            var originTraceId = ExtractTraceId(evt);
            if (!string.IsNullOrEmpty(originTraceId))
            {
                try
                {
                    var traceId = ActivityTraceId.CreateFromString(originTraceId.AsSpan());
                    var linkedContext = new ActivityContext(traceId, default, ActivityTraceFlags.Recorded);
                    links.Add(new ActivityLink(linkedContext, new ActivityTagsCollection
                    {
                        { "link.description", "Trace that originated this database change" }
                    }));

                    _logger.LogInformation("CDC Event linked to origin TraceId: {TraceId}", originTraceId);
                }
                catch
                {
                    // TraceId inválido — ignora o link
                }
            }

            // Gera span com link para o trace original (se disponível)
            using var activity = CdcActivitySource.StartActivity(
                $"CDC {evt.Operation} {evt.Table}",
                ActivityKind.Consumer,
                parentContext: default,
                links: links);

            if (activity != null)
            {
                activity.SetTag("cdc.operation", evt.Operation);
                activity.SetTag("cdc.table", evt.Table);
                activity.SetTag("cdc.timestamp", evt.Timestamp);
                activity.SetTag("cdc.source", "postgresql_notify");
                activity.SetTag("cdc.has_origin_link", !string.IsNullOrEmpty(originTraceId));

                if (!string.IsNullOrEmpty(originTraceId))
                    activity.SetTag("cdc.origin_trace_id", originTraceId);

                activity.SetTag("cdc.payload", payload.Length > 2000
                    ? payload.Substring(0, 2000) + "..."
                    : payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing CDC event: {Payload}", payload);
        }
    }

    /// <summary>
    /// Extrai o TraceId do campo data do payload CDC.
    /// O TraceId é gravado na tabela pela procedure (ex: Quotes.TraceId).
    /// </summary>
    private static string? ExtractTraceId(CdcEvent evt)
    {
        if (evt.Data == null) return null;

        try
        {
            // O trigger envia row_to_json(NEW) — o campo TraceId vem como propriedade
            if (evt.Data.Value.TryGetProperty("TraceId", out var traceIdProp))
                return traceIdProp.GetString();
        }
        catch { }

        return null;
    }
}

public class CdcEvent
{
    public string Operation { get; set; } = "";
    public string Table { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public JsonElement? Data { get; set; }
}
