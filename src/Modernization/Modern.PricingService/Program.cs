using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;
using SeguroAuto.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSeguroAutoData(builder.Configuration);

var app = builder.Build();

await app.Services.SeedDatabaseAsync();

// Microsserviço de Pricing — extraído do monólito Legacy.PricingRulesService
// Demonstra decomposição: funcionalidade extraída para serviço independente

app.MapGet("/api/pricing/rules", async (SeguroAutoDbContext context) =>
{
    var rules = await context.PricingRules.ToListAsync();
    return Results.Ok(rules.Select(r => new
    {
        r.Id,
        r.Name,
        r.Description,
        r.Multiplier,
        r.Condition,
        r.IsActive
    }));
});

app.MapGet("/api/pricing/rules/active", async (SeguroAutoDbContext context) =>
{
    var rules = await context.PricingRules
        .Where(r => r.IsActive)
        .ToListAsync();
    return Results.Ok(rules.Select(r => new
    {
        r.Id,
        r.Name,
        r.Multiplier,
        r.Condition
    }));
});

app.MapGet("/api/pricing/calculate", async (
    [FromQuery] int vehicleYear,
    [FromQuery] int customerId,
    SeguroAutoDbContext context,
    ILogger<Program> logger) =>
{
    logger.LogInformation("[PricingService] Calculate premium for VehicleYear: {Year}, CustomerId: {Id}",
        vehicleYear, customerId);

    var basePremium = 1000m;
    var age = DateTime.Now.Year - vehicleYear;
    if (age < 3) basePremium = 1200m;
    else if (age >= 10) basePremium = 1500m;

    var rules = await context.PricingRules.Where(r => r.IsActive).ToListAsync();
    var finalPremium = basePremium;
    var appliedRules = new List<object>();

    foreach (var rule in rules)
    {
        var applied = false;
        if (rule.Condition.Contains("VehicleYear <") && age > 10) applied = true;
        if (rule.Condition.Contains("VehicleYear >=") && age <= 2) applied = true;
        if (rule.Condition.Contains("Customer.Policies.Count"))
        {
            var policyCount = await context.Policies.CountAsync(p => p.CustomerId == customerId);
            if (policyCount > 2) applied = true;
        }

        if (applied)
        {
            finalPremium *= rule.Multiplier;
            appliedRules.Add(new { rule.Name, rule.Multiplier });
        }
    }

    return Results.Ok(new
    {
        basePremium,
        finalPremium = Math.Round(finalPremium, 2),
        appliedRules,
        vehicleYear,
        customerId
    });
});

app.MapDefaultEndpoints();

app.Run();
