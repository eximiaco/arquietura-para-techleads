using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SeguroAuto.Data;

namespace Modern.CdcWorker;

/// <summary>
/// Cria triggers PostgreSQL para Change Data Capture via LISTEN/NOTIFY.
/// Os triggers notificam o canal 'cdc_events' quando dados mudam nas tabelas monitoradas.
/// </summary>
public static class CdcSetup
{
    public static async Task EnsureTriggersAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SeguroAutoDbContext>();

        // Função genérica de notificação CDC
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE OR REPLACE FUNCTION cdc_notify_change()
            RETURNS trigger AS $$
            DECLARE
                payload json;
                record_data json;
            BEGIN
                IF (TG_OP = 'DELETE') THEN
                    record_data = row_to_json(OLD);
                ELSE
                    record_data = row_to_json(NEW);
                END IF;

                payload = json_build_object(
                    'operation', TG_OP,
                    'table', TG_TABLE_NAME,
                    'timestamp', NOW()::text,
                    'data', record_data
                );

                PERFORM pg_notify('cdc_events', payload::text);
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
        ");

        // Triggers nas tabelas monitoradas
        var tables = new[] { "Quotes", "Policies", "Claims" };
        foreach (var table in tables)
        {
            var triggerName = $"cdc_trigger_{table.ToLower()}";
            await context.Database.ExecuteSqlRawAsync($@"
                DROP TRIGGER IF EXISTS {triggerName} ON ""{table}"";
                CREATE TRIGGER {triggerName}
                    AFTER INSERT OR UPDATE OR DELETE ON ""{table}""
                    FOR EACH ROW EXECUTE FUNCTION cdc_notify_change();
            ");
        }
    }
}
