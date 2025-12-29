using Microsoft.EntityFrameworkCore;
using SeguroAuto.Domain;

namespace SeguroAuto.Data;

public class DatabaseSeeder
{
    private readonly SeguroAutoDbContext _context;
    private readonly int _seed;
    private readonly string _profile;

    public DatabaseSeeder(SeguroAutoDbContext context, int seed, string profile)
    {
        _context = context;
        _seed = seed;
        _profile = profile;
    }

    public async Task SeedAsync()
    {
        // Verifica se já existe dados (seeding idempotente)
        if (await _context.Customers.AnyAsync())
        {
            return;
        }

        var random = new Random(_seed);

        // IDs âncora sempre presentes
        var anchorCustomer = new Customer
        {
            Id = 999,
            Name = "Cliente Âncora",
            Email = "ancora@example.com",
            Document = "12345678900",
            CreatedAt = DateTime.UtcNow.AddDays(-365)
        };

        var anchorPolicy = new Policy
        {
            Id = 1234,
            PolicyNumber = "AUTO-1234",
            CustomerId = 999,
            VehiclePlate = "ABC-1234",
            VehicleModel = "Honda Civic",
            VehicleYear = 2020,
            Premium = 1500.00m,
            StartDate = DateTime.UtcNow.AddMonths(-6),
            EndDate = DateTime.UtcNow.AddMonths(6),
            Status = PolicyStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        _context.Customers.Add(anchorCustomer);
        _context.Policies.Add(anchorPolicy);

        // Gera dados adicionais baseados no seed
        var customers = new List<Customer> { anchorCustomer };
        var policies = new List<Policy> { anchorPolicy };

        // Cria mais clientes
        for (int i = 1; i <= 20; i++)
        {
            if (i == 999) continue; // Pula o ID âncora

            customers.Add(new Customer
            {
                Id = i,
                Name = $"Cliente {i}",
                Email = $"cliente{i}@example.com",
                Document = $"{10000000000 + i + _seed}",
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365))
            });
        }

        // Cria mais políticas
        for (int i = 1; i <= 15; i++)
        {
            if (i == 1234) continue; // Pula o ID âncora

            var customer = customers[random.Next(customers.Count)];
            policies.Add(new Policy
            {
                Id = 1000 + i,
                PolicyNumber = $"AUTO-{1000 + i}",
                CustomerId = customer.Id,
                VehiclePlate = $"{GetRandomLetters(random, 3)}-{random.Next(1000, 9999)}",
                VehicleModel = GetRandomVehicleModel(random),
                VehicleYear = random.Next(2010, 2025),
                Premium = random.Next(800, 3000),
                StartDate = DateTime.UtcNow.AddMonths(-random.Next(1, 12)),
                EndDate = DateTime.UtcNow.AddMonths(random.Next(1, 12)),
                Status = (PolicyStatus)random.Next(0, 4),
                CreatedAt = DateTime.UtcNow.AddMonths(-random.Next(1, 12))
            });
        }

        // Cria quotes
        var quotes = new List<Quote>();
        for (int i = 1; i <= 10; i++)
        {
            var customer = customers[random.Next(customers.Count)];
            quotes.Add(new Quote
            {
                Id = i,
                QuoteNumber = $"QUOTE-{i}",
                CustomerId = customer.Id,
                VehiclePlate = $"{GetRandomLetters(random, 3)}-{random.Next(1000, 9999)}",
                VehicleModel = GetRandomVehicleModel(random),
                VehicleYear = random.Next(2010, 2025),
                Premium = random.Next(700, 2800),
                Status = (QuoteStatus)random.Next(0, 5),
                ValidUntil = DateTime.UtcNow.AddDays(random.Next(1, 30)),
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
            });
        }

        // Cria claims
        var claims = new List<Claim>();
        for (int i = 1; i <= 8; i++)
        {
            var policy = policies[random.Next(policies.Count)];
            claims.Add(new Claim
            {
                Id = i,
                ClaimNumber = $"CLAIM-{i}",
                PolicyId = policy.Id,
                Description = $"Sinistro {i}: {GetRandomClaimDescription(random)}",
                Amount = random.Next(500, 5000),
                Status = (ClaimStatus)random.Next(0, 5),
                IncidentDate = DateTime.UtcNow.AddDays(-random.Next(1, 180)),
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 180))
            });
        }

        // Cria pricing rules
        var pricingRules = new List<PricingRule>
        {
            new PricingRule
            {
                Id = 1,
                Name = "Veículo Antigo",
                Description = "Aplica multiplicador para veículos com mais de 10 anos",
                Multiplier = 1.2m,
                Condition = "VehicleYear < (CurrentYear - 10)",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            },
            new PricingRule
            {
                Id = 2,
                Name = "Veículo Novo",
                Description = "Desconto para veículos novos",
                Multiplier = 0.9m,
                Condition = "VehicleYear >= (CurrentYear - 2)",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            },
            new PricingRule
            {
                Id = 3,
                Name = "Cliente Fiel",
                Description = "Desconto para clientes com múltiplas políticas",
                Multiplier = 0.85m,
                Condition = "Customer.Policies.Count > 2",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            }
        };

        _context.Customers.AddRange(customers.Skip(1)); // Pula o primeiro (âncora já adicionado)
        _context.Policies.AddRange(policies.Skip(1)); // Pula o primeiro (âncora já adicionado)
        _context.Quotes.AddRange(quotes);
        _context.Claims.AddRange(claims);
        _context.PricingRules.AddRange(pricingRules);

        await _context.SaveChangesAsync();
    }

    private static string GetRandomLetters(Random random, int count)
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return new string(Enumerable.Range(0, count)
            .Select(_ => letters[random.Next(letters.Length)])
            .ToArray());
    }

    private static string GetRandomVehicleModel(Random random)
    {
        var models = new[]
        {
            "Honda Civic", "Toyota Corolla", "Volkswagen Gol",
            "Fiat Uno", "Chevrolet Onix", "Ford Ka",
            "Renault Kwid", "Hyundai HB20", "Nissan Versa"
        };
        return models[random.Next(models.Length)];
    }

    private static string GetRandomClaimDescription(Random random)
    {
        var descriptions = new[]
        {
            "Colisão traseira", "Batida lateral", "Capotamento",
            "Colisão frontal", "Arranhão", "Quebra de vidro",
            "Furto parcial", "Incêndio"
        };
        return descriptions[random.Next(descriptions.Length)];
    }
}

