using System.ServiceModel;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;
using SeguroAuto.Domain;

namespace Legacy.QuoteService;

public class QuoteService : IQuoteService
{
    private readonly SeguroAutoDbContext _context;
    private readonly ILogger<QuoteService> _logger;

    public QuoteService(SeguroAutoDbContext context, ILogger<QuoteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public QuoteResponse GetQuote(QuoteRequest request)
    {
        _logger.LogInformation("GetQuote called for CustomerId: {CustomerId}, Vehicle: {VehicleModel}", 
            request.CustomerId, request.VehicleModel);

        var customer = _context.Customers.Find(request.CustomerId);
        if (customer == null)
        {
            throw new FaultException("Customer not found");
        }

        // Calcula prêmio base
        var basePremium = CalculateBasePremium(request.VehicleYear, request.VehicleModel);
        
        // Aplica regras de pricing
        var finalPremium = ApplyPricingRules(basePremium, request, customer);

        var quote = new Quote
        {
            QuoteNumber = $"QUOTE-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CustomerId = request.CustomerId,
            VehiclePlate = request.VehiclePlate,
            VehicleModel = request.VehicleModel,
            VehicleYear = request.VehicleYear,
            Premium = finalPremium,
            Status = QuoteStatus.Pending,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };

        _context.Quotes.Add(quote);
        _context.SaveChanges();

        return new QuoteResponse
        {
            QuoteNumber = quote.QuoteNumber,
            CustomerId = quote.CustomerId,
            Premium = quote.Premium,
            ValidUntil = quote.ValidUntil,
            Status = quote.Status.ToString()
        };
    }

    public QuoteResponse[] GetQuotesByCustomer(int customerId)
    {
        _logger.LogInformation("GetQuotesByCustomer called for CustomerId: {CustomerId}", customerId);

        var quotes = _context.Quotes
            .Where(q => q.CustomerId == customerId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(10)
            .ToList();

        return quotes.Select(q => new QuoteResponse
        {
            QuoteNumber = q.QuoteNumber,
            CustomerId = q.CustomerId,
            Premium = q.Premium,
            ValidUntil = q.ValidUntil,
            Status = q.Status.ToString()
        }).ToArray();
    }

    public bool ApproveQuote(string quoteNumber)
    {
        _logger.LogInformation("ApproveQuote called for QuoteNumber: {QuoteNumber}", quoteNumber);

        var quote = _context.Quotes.FirstOrDefault(q => q.QuoteNumber == quoteNumber);
        if (quote == null)
        {
            return false;
        }

        quote.Status = QuoteStatus.Approved;
        _context.SaveChanges();

        return true;
    }

    private decimal CalculateBasePremium(int vehicleYear, string vehicleModel)
    {
        var age = DateTime.Now.Year - vehicleYear;
        var basePremium = 1000m;

        if (age < 3)
            basePremium = 1200m;
        else if (age < 10)
            basePremium = 1000m;
        else
            basePremium = 1500m;

        return basePremium;
    }

    private decimal ApplyPricingRules(decimal basePremium, QuoteRequest request, Customer customer)
    {
        var rules = _context.PricingRules.Where(r => r.IsActive).ToList();
        var finalPremium = basePremium;

        foreach (var rule in rules)
        {
            if (ShouldApplyRule(rule, request, customer))
            {
                finalPremium *= rule.Multiplier;
            }
        }

        return Math.Round(finalPremium, 2);
    }

    private bool ShouldApplyRule(PricingRule rule, QuoteRequest request, Customer customer)
    {
        // Implementação simplificada - em produção seria mais complexa
        if (rule.Condition.Contains("VehicleYear <"))
        {
            var age = DateTime.Now.Year - request.VehicleYear;
            return age > 10;
        }

        if (rule.Condition.Contains("VehicleYear >="))
        {
            var age = DateTime.Now.Year - request.VehicleYear;
            return age <= 2;
        }

        if (rule.Condition.Contains("Customer.Policies.Count"))
        {
            var policyCount = _context.Policies.Count(p => p.CustomerId == customer.Id);
            return policyCount > 2;
        }

        return false;
    }
}

