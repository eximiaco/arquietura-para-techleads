using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;

namespace Legacy.DbTelemetryWorker;

public class DbTelemetryBackgroundService : BackgroundService
{
    private static readonly ActivitySource DbActivitySource = new("SeguroAuto.Database");
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DbTelemetryBackgroundService> _logger;

    public DbTelemetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DbTelemetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DbTelemetryWorker started. Polling db_operation_logs every 2s...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingLogsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing db operation logs");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task ProcessPendingLogsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SeguroAutoDbContext>();

        var pendingLogs = await context.DbOperationLogs
            .Where(l => !l.Exported)
            .OrderBy(l => l.Id)
            .Take(50)
            .ToListAsync(ct);

        if (pendingLogs.Count == 0) return;

        _logger.LogInformation("Processing {Count} pending db operation logs", pendingLogs.Count);

        foreach (var log in pendingLogs)
        {
            EmitSpanFromLog(log);
            log.Exported = true;
        }

        await context.SaveChangesAsync(ct);
    }

    private void EmitSpanFromLog(DbOperationLog log)
    {
        // Reconstrói o trace context original para vincular o span como filho
        ActivityContext parentContext = default;
        try
        {
            var traceId = ActivityTraceId.CreateFromString(log.TraceId.AsSpan());
            var spanId = ActivitySpanId.CreateFromString(log.SpanId.AsSpan());
            parentContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
        }
        catch
        {
            _logger.LogWarning("Invalid trace context in log {Id}: TraceId={TraceId}, SpanId={SpanId}",
                log.Id, log.TraceId, log.SpanId);
            return;
        }

        // Cria span com o parent context original - aparece dentro do trace do serviço
        using var activity = DbActivitySource.StartActivity(
            log.OperationName,
            ActivityKind.Internal,
            parentContext);

        if (activity == null) return;

        activity.SetStartTime(log.StartedAt.ToUniversalTime());

        // Tags semânticas OpenTelemetry para operações de banco
        activity.SetTag("db.system", "postgresql");
        activity.SetTag("db.operation", log.OperationType);
        activity.SetTag("db.sql.table", log.TableName);
        activity.SetTag("db.operation.name", log.OperationName);

        if (!string.IsNullOrEmpty(log.Details))
            activity.SetTag("db.operation.details", log.Details);

        if (log.Status == "ERROR")
        {
            activity.SetStatus(ActivityStatusCode.Error, log.ErrorMessage);
        }

        activity.SetEndTime(log.EndedAt.ToUniversalTime());
    }
}
