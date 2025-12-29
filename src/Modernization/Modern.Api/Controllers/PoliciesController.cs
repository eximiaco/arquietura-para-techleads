using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;
using SeguroAuto.Domain;

namespace Modern.Api.Controllers;

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

    [HttpGet("{policyNumber}")]
    public async Task<ActionResult<PolicyDto>> GetPolicy(string policyNumber)
    {
        var policy = await _context.Policies
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.PolicyNumber == policyNumber);

        if (policy == null)
        {
            return NotFound();
        }

        return Ok(new PolicyDto
        {
            PolicyNumber = policy.PolicyNumber,
            CustomerId = policy.CustomerId,
            VehiclePlate = policy.VehiclePlate,
            Premium = policy.Premium,
            StartDate = policy.StartDate,
            EndDate = policy.EndDate,
            Status = policy.Status.ToString()
        });
    }

    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<PolicyDto[]>> GetPoliciesByCustomer(int customerId)
    {
        var policies = await _context.Policies
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return Ok(policies.Select(p => new PolicyDto
        {
            PolicyNumber = p.PolicyNumber,
            CustomerId = p.CustomerId,
            VehiclePlate = p.VehiclePlate,
            Premium = p.Premium,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            Status = p.Status.ToString()
        }).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<PolicyDto>> CreatePolicy([FromBody] CreatePolicyRequest request)
    {
        var customer = await _context.Customers.FindAsync(request.CustomerId);
        if (customer == null)
        {
            return NotFound(new { error = "Customer not found" });
        }

        var policy = new Policy
        {
            PolicyNumber = $"AUTO-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CustomerId = request.CustomerId,
            VehiclePlate = request.VehiclePlate,
            VehicleModel = request.VehicleModel,
            VehicleYear = request.VehicleYear,
            Premium = request.Premium,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddYears(1),
            Status = PolicyStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _context.Policies.Add(policy);
        await _context.SaveChangesAsync();

        return Ok(new PolicyDto
        {
            PolicyNumber = policy.PolicyNumber,
            CustomerId = policy.CustomerId,
            VehiclePlate = policy.VehiclePlate,
            Premium = policy.Premium,
            StartDate = policy.StartDate,
            EndDate = policy.EndDate,
            Status = policy.Status.ToString()
        });
    }
}

public class CreatePolicyRequest
{
    public int CustomerId { get; set; }
    public string VehiclePlate { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public int VehicleYear { get; set; }
    public decimal Premium { get; set; }
}

public class PolicyDto
{
    public string PolicyNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string VehiclePlate { get; set; } = string.Empty;
    public decimal Premium { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

