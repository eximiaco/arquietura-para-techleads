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

