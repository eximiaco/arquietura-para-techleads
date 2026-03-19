using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;

namespace Modern.Api.Controllers;

/// <summary>
/// API REST moderna para apólices (somente leitura).
/// CQRS: Queries via REST moderno, Commands via SOAP legado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    private readonly SeguroAutoDbContext _context;
    private readonly ILogger<PoliciesController> _logger;

    public PoliciesController(SeguroAutoDbContext context, ILogger<PoliciesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/policies/customer/{customerId}
    /// Query: listar apólices por cliente (substitui GetPoliciesByCustomer SOAP).
    /// </summary>
    [HttpGet("customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId)
    {
        _logger.LogInformation("[Modern.Api] Policies.GetByCustomer for CustomerId: {CustomerId}", customerId);

        var policies = await _context.Policies
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PolicyDto
            {
                PolicyNumber = p.PolicyNumber,
                CustomerId = p.CustomerId,
                VehiclePlate = p.VehiclePlate,
                VehicleModel = p.VehicleModel,
                VehicleYear = p.VehicleYear,
                Premium = p.Premium,
                Status = p.Status.ToString(),
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(policies);
    }

    /// <summary>
    /// GET /api/policies/{policyNumber}
    /// Query: consultar apólice individual.
    /// </summary>
    [HttpGet("{policyNumber}")]
    public async Task<IActionResult> GetByNumber(string policyNumber)
    {
        _logger.LogInformation("[Modern.Api] Policies.GetByNumber for PolicyNumber: {PolicyNumber}", policyNumber);

        var policy = await _context.Policies
            .Where(p => p.PolicyNumber == policyNumber)
            .Select(p => new PolicyDto
            {
                PolicyNumber = p.PolicyNumber,
                CustomerId = p.CustomerId,
                VehiclePlate = p.VehiclePlate,
                VehicleModel = p.VehicleModel,
                VehicleYear = p.VehicleYear,
                Premium = p.Premium,
                Status = p.Status.ToString(),
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                CreatedAt = p.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (policy == null)
            return NotFound(new { error = "Policy not found", policyNumber });

        return Ok(policy);
    }
}

public class PolicyDto
{
    public string PolicyNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public string VehiclePlate { get; set; } = "";
    public string VehicleModel { get; set; } = "";
    public int VehicleYear { get; set; }
    public decimal Premium { get; set; }
    public string Status { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
