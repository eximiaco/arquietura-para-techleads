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

        // Executa seeding
        var seed = int.Parse(configuration["DATASET_SEED"] ?? "1001");
        var profile = configuration["DATASET_PROFILE"] ?? "legacy";
        var seeder = new DatabaseSeeder(context, seed, profile);
        await seeder.SeedAsync();

        return serviceProvider;
    }
}

