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
        // O Aspire injeta a connection string via ConnectionStrings__legacydb
        var connectionString = configuration.GetConnectionString("legacydb");

        services.AddDbContext<SeguroAutoDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }

    public static async Task<IServiceProvider> SeedDatabaseAsync(
        this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SeguroAutoDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Garante que o banco e todas as tabelas estão criados (inclui db_operation_logs via DbSet)
        await context.Database.EnsureCreatedAsync();

        // Executa seeding
        var seed = int.Parse(configuration["DATASET_SEED"] ?? "1001");
        var profile = configuration["DATASET_PROFILE"] ?? "legacy";
        var seeder = new DatabaseSeeder(context, seed, profile);
        await seeder.SeedAsync();

        // PostgreSQL: após seeding com IDs explícitos, reseta as sequences
        // para evitar conflito de PK ao inserir novos registros
        await ResetPostgresSequencesAsync(context);

        return serviceProvider;
    }

    /// <summary>
    /// O seeder insere registros com Id explícito (ex: Id=999, Id=1234).
    /// No PostgreSQL, a sequence não avança automaticamente nesses casos.
    /// Este método reseta cada sequence para MAX(Id)+1 da respectiva tabela.
    /// </summary>
    private static async Task ResetPostgresSequencesAsync(SeguroAutoDbContext context)
    {
        var tables = new[] { "Customers", "Policies", "Claims", "Quotes", "PricingRules" };

        foreach (var table in tables)
        {
            // Formato da sequence gerada pelo EF Core/Npgsql: "{Table}_{Column}_seq"
            var sequenceName = $"\"{table}_Id_seq\"";
            await context.Database.ExecuteSqlRawAsync(
                $"SELECT setval('{sequenceName}', COALESCE((SELECT MAX(\"Id\") FROM \"{table}\"), 0) + 1, false)");
        }
    }
}
