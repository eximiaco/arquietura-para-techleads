using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;
using SeguroAuto.Domain;

namespace Modern.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    private readonly SeguroAutoDbContext _context;
    private readonly ILogger<QuotesController> _logger;

    public QuotesController(SeguroAutoDbContext context, ILogger<QuotesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<QuoteDto>> CreateQuote([FromBody] CreateQuoteRequest request)
    {
        var customer = await _context.Customers.FindAsync(request.CustomerId);
        if (customer == null)
        {
            return NotFound(new { error = "Customer not found" });
        }

        var basePremium = CalculateBasePremium(request.VehicleYear, request.VehicleModel);
        var finalPremium = await ApplyPricingRulesAsync(basePremium, request, customer);

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
        await _context.SaveChangesAsync();

        return Ok(new QuoteDto
        {
            QuoteNumber = quote.QuoteNumber,
            CustomerId = quote.CustomerId,
            Premium = quote.Premium,
            ValidUntil = quote.ValidUntil,
            Status = quote.Status.ToString()
        });
    }

    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<QuoteDto[]>> GetQuotesByCustomer(int customerId)
    {
        var quotes = await _context.Quotes
            .Where(q => q.CustomerId == customerId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(10)
            .ToListAsync();

        return Ok(quotes.Select(q => new QuoteDto
        {
            QuoteNumber = q.QuoteNumber,
            CustomerId = q.CustomerId,
            Premium = q.Premium,
            ValidUntil = q.ValidUntil,
            Status = q.Status.ToString()
        }).ToArray());
    }

    [HttpPost("{quoteNumber}/approve")]
    public async Task<ActionResult> ApproveQuote(string quoteNumber)
    {
        var quote = await _context.Quotes.FirstOrDefaultAsync(q => q.QuoteNumber == quoteNumber);
        if (quote == null)
        {
            return NotFound();
        }

        quote.Status = QuoteStatus.Approved;
        await _context.SaveChangesAsync();

        return Ok();
    }

    private decimal CalculateBasePremium(int vehicleYear, string vehicleModel)
    {
        var age = DateTime.Now.Year - vehicleYear;
        return age < 3 ? 1200m : age < 10 ? 1000m : 1500m;
    }

    private async Task<decimal> ApplyPricingRulesAsync(decimal basePremium, CreateQuoteRequest request, Customer customer)
    {
        var rules = await _context.PricingRules.Where(r => r.IsActive).ToListAsync();
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

    private bool ShouldApplyRule(PricingRule rule, CreateQuoteRequest request, Customer customer)
    {
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

public class CreateQuoteRequest
{
    public int CustomerId { get; set; }
    public string VehiclePlate { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public int VehicleYear { get; set; }
}

public class QuoteDto
{
    public string QuoteNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public decimal Premium { get; set; }
    public DateTime ValidUntil { get; set; }
    public string Status { get; set; } = string.Empty;
}

