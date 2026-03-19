using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;

namespace Modern.Api.Controllers;

/// <summary>
/// API REST moderna para sinistros (somente leitura).
/// CQRS: Queries via REST moderno, Commands via SOAP legado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ClaimsController : ControllerBase
{
    private readonly SeguroAutoDbContext _context;
    private readonly ILogger<ClaimsController> _logger;

    public ClaimsController(SeguroAutoDbContext context, ILogger<ClaimsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/claims/policy/{policyNumber}
    /// Query: listar sinistros por apólice.
    /// </summary>
    [HttpGet("policy/{policyNumber}")]
    public async Task<IActionResult> GetByPolicy(string policyNumber)
    {
        _logger.LogInformation("[Modern.Api] Claims.GetByPolicy for PolicyNumber: {PolicyNumber}", policyNumber);

        var policy = await _context.Policies
            .FirstOrDefaultAsync(p => p.PolicyNumber == policyNumber);

        if (policy == null)
            return NotFound(new { error = "Policy not found", policyNumber });

        var claims = await _context.Claims
            .Where(c => c.PolicyId == policy.Id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ClaimDto
            {
                ClaimNumber = c.ClaimNumber,
                PolicyNumber = policyNumber,
                Description = c.Description,
                Amount = c.Amount,
                Status = c.Status.ToString(),
                IncidentDate = c.IncidentDate,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(claims);
    }
}

public class ClaimDto
{
    public string ClaimNumber { get; set; } = "";
    public string PolicyNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public DateTime IncidentDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
