using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Modern.Api.AntiCorruption;
using SeguroAuto.Data;
using SeguroAuto.Domain;

namespace Modern.Api.Controllers;

/// <summary>
/// API REST moderna para cotações.
/// - Queries (GET): acesso direto ao banco (CQRS)
/// - Commands (POST): via Anti-Corruption Layer → Legacy SOAP (ACL)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    private readonly SeguroAutoDbContext _context;
    private readonly LegacyQuoteAdapter _legacyAdapter;
    private readonly ILogger<QuotesController> _logger;

    public QuotesController(SeguroAutoDbContext context, LegacyQuoteAdapter legacyAdapter, ILogger<QuotesController> logger)
    {
        _context = context;
        _legacyAdapter = legacyAdapter;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/quotes/customer/{customerId}
    /// Substitui o GetQuotesByCustomer do SOAP legado.
    /// </summary>
    [HttpGet("customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        _logger.LogInformation("[Modern.Api] GetByCustomer called for CustomerId: {CustomerId}", customerId);

        var quotes = await _context.Quotes
            .Where(q => q.CustomerId == customerId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(10)
            .Select(q => new QuoteDto
            {
                QuoteNumber = q.QuoteNumber,
                CustomerId = q.CustomerId,
                VehiclePlate = q.VehiclePlate,
                VehicleModel = q.VehicleModel,
                VehicleYear = q.VehicleYear,
                Premium = q.Premium,
                Status = q.Status.ToString(),
                ValidUntil = q.ValidUntil,
                CreatedAt = q.CreatedAt
            })
            .ToListAsync();

        _logger.LogInformation("[Modern.Api] Found {Count} quotes for CustomerId: {CustomerId}", quotes.Count, customerId);

        return Ok(quotes);
    }

    /// <summary>
    /// GET /api/quotes/{quoteNumber}
    /// Consulta individual — não existe no SOAP legado.
    /// </summary>
    [HttpGet("{quoteNumber}")]
    public async Task<IActionResult> GetByNumber(string quoteNumber)
    {
        _logger.LogInformation("[Modern.Api] GetByNumber called for QuoteNumber: {QuoteNumber}", quoteNumber);

        var quote = await _context.Quotes
            .Where(q => q.QuoteNumber == quoteNumber)
            .Select(q => new QuoteDto
            {
                QuoteNumber = q.QuoteNumber,
                CustomerId = q.CustomerId,
                VehiclePlate = q.VehiclePlate,
                VehicleModel = q.VehicleModel,
                VehicleYear = q.VehicleYear,
                Premium = q.Premium,
                Status = q.Status.ToString(),
                ValidUntil = q.ValidUntil,
                CreatedAt = q.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (quote == null)
            return NotFound(new { error = "Quote not found", quoteNumber });

        return Ok(quote);
    }

    /// <summary>
    /// POST /api/quotes
    /// Cria cotação via Anti-Corruption Layer → delega ao Legacy SOAP QuoteService.
    /// O Modern.Api NÃO conhece SOAP — o ACL faz a tradução.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuoteCommand command)
    {
        _logger.LogInformation("[Modern.Api] Create quote via ACL for CustomerId: {CustomerId}", command.CustomerId);

        try
        {
            var result = await _legacyAdapter.CreateQuoteAsync(command);
            return Created($"/api/quotes/{result.QuoteNumber}", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Modern.Api] Error creating quote via ACL");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class QuoteDto
{
    public string QuoteNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public string VehiclePlate { get; set; } = "";
    public string VehicleModel { get; set; } = "";
    public int VehicleYear { get; set; }
    public decimal Premium { get; set; }
    public string Status { get; set; } = "";
    public DateTime ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}
