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
        try
        {
            _logger.LogInformation("GetQuote called for CustomerId: {CustomerId}, Vehicle: {VehicleModel}", 
                request.CustomerId, request.VehicleModel);

            var customer = _context.Customers.Find(request.CustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
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

            _logger.LogInformation("Quote created: {QuoteNumber} for CustomerId: {CustomerId} with Premium: {Premium}", 
                quote.QuoteNumber, request.CustomerId, quote.Premium);

            return new QuoteResponse
            {
                QuoteNumber = quote.QuoteNumber,
                CustomerId = quote.CustomerId,
                Premium = quote.Premium,
                ValidUntil = quote.ValidUntil,
                Status = quote.Status.ToString()
            };
        }
        catch (FaultException)
        {
            throw; // Re-throw FaultException as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetQuote for CustomerId: {CustomerId}", request.CustomerId);
            throw new FaultException($"Error creating quote: {ex.Message}");
        }
    }

    public GetQuotesByCustomerResponse GetQuotesByCustomer(GetQuotesByCustomerRequest request)
    {
        try
        {
            _logger.LogInformation("GetQuotesByCustomer called for CustomerId: {CustomerId}", request.CustomerId);

            var quotes = _context.Quotes
                .Where(q => q.CustomerId == request.CustomerId)
                .OrderByDescending(q => q.CreatedAt)
                .Take(10)
                .ToList();

            _logger.LogInformation("Found {Count} quotes for CustomerId: {CustomerId}", quotes.Count, request.CustomerId);

            return new GetQuotesByCustomerResponse
            {
                Quotes = quotes.Select(q => new QuoteResponse
                {
                    QuoteNumber = q.QuoteNumber,
                    CustomerId = q.CustomerId,
                    Premium = q.Premium,
                    ValidUntil = q.ValidUntil,
                    Status = q.Status.ToString()
                }).ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetQuotesByCustomer for CustomerId: {CustomerId}", request.CustomerId);
            throw new FaultException($"Error retrieving quotes: {ex.Message}");
        }
    }

    public ApproveQuoteResponse ApproveQuote(ApproveQuoteRequest request)
    {
        try
        {
            _logger.LogInformation("ApproveQuote called for QuoteNumber: {QuoteNumber}", request.QuoteNumber);

            var quote = _context.Quotes.FirstOrDefault(q => q.QuoteNumber == request.QuoteNumber);
            if (quote == null)
            {
                _logger.LogWarning("Quote not found: {QuoteNumber}", request.QuoteNumber);
                return new ApproveQuoteResponse { Success = false };
            }

            quote.Status = QuoteStatus.Approved;
            _context.SaveChanges();

            _logger.LogInformation("Quote approved: {QuoteNumber}", request.QuoteNumber);

            return new ApproveQuoteResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ApproveQuote for QuoteNumber: {QuoteNumber}", request.QuoteNumber);
            throw new FaultException($"Error approving quote: {ex.Message}");
        }
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

