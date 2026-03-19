using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Modern.CdcWorker;

/// <summary>
/// Worker que escuta eventos CDC do PostgreSQL via LISTEN/NOTIFY.
/// Cada evento de mudança (INSERT/UPDATE/DELETE) gera um span no tracing,
/// simulando como um sistema de CDC (Debezium, etc.) capturaria mudanças.
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

                // Escuta o canal de eventos CDC
                await using var cmd = new NpgsqlCommand("LISTEN cdc_events", connection);
                await cmd.ExecuteNonQueryAsync(stoppingToken);

                connection.Notification += (_, args) =>
                {
                    ProcessCdcEvent(args.Payload);
                };

                // Loop de espera por notificações
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

            // Gera span no tracing para cada evento CDC
            using var activity = CdcActivitySource.StartActivity(
                $"CDC {evt.Operation} {evt.Table}",
                ActivityKind.Consumer);

            if (activity != null)
            {
                activity.SetTag("cdc.operation", evt.Operation);
                activity.SetTag("cdc.table", evt.Table);
                activity.SetTag("cdc.timestamp", evt.Timestamp);
                activity.SetTag("cdc.source", "postgresql_notify");
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
}

public class CdcEvent
{
    public string Operation { get; set; } = "";
    public string Table { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public JsonElement? Data { get; set; }
}
