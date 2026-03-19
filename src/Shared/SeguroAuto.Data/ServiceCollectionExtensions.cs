using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SeguroAuto.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSeguroAutoData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbPath = configuration["DB_PATH"] ?? "./data/legacy.db";
        
        // Converte caminho relativo para absoluto e garante que o diretório existe
        if (!Path.IsPathRooted(dbPath))
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            dbPath = Path.GetFullPath(Path.Combine(baseDirectory, dbPath));
        }
        
        // Garante que o diretório do banco existe
        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }
        
        var connectionString = $"Data Source={dbPath}";

        services.AddDbContext<SeguroAutoDbContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }

    public static async Task<IServiceProvider> SeedDatabaseAsync(
        this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SeguroAutoDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Garante que o banco está criado
        await context.Database.EnsureCreatedAsync();

        // Garante que a tabela de telemetria existe (idempotente para DBs pré-existentes)
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS db_operation_logs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TraceId TEXT NOT NULL,
                SpanId TEXT NOT NULL,
                OperationName TEXT NOT NULL,
                OperationType TEXT NOT NULL,
                TableName TEXT NOT NULL,
                Details TEXT,
                StartedAt TEXT NOT NULL,
                EndedAt TEXT NOT NULL,
                Status TEXT NOT NULL DEFAULT 'OK',
                ErrorMessage TEXT,
                Exported INTEGER NOT NULL DEFAULT 0
            )");
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS ix_db_operation_logs_exported
            ON db_operation_logs (Exported) WHERE Exported = 0");

        // Executa seeding
        var seed = int.Parse(configuration["DATASET_SEED"] ?? "1001");
        var profile = configuration["DATASET_PROFILE"] ?? "legacy";
        var seeder = new DatabaseSeeder(context, seed, profile);
        await seeder.SeedAsync();

        return serviceProvider;
    }
}

