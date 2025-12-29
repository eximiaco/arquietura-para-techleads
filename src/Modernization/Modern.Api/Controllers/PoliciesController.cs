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
        try
        {
            _logger.LogInformation("GetPolicy called for PolicyNumber: {PolicyNumber}", policyNumber);

            var policy = await _context.Policies
                .Include(p => p.Customer)
                .FirstOrDefaultAsync(p => p.PolicyNumber == policyNumber);

            if (policy == null)
            {
                _logger.LogWarning("Policy not found: {PolicyNumber}", policyNumber);
                return NotFound();
            }

            _logger.LogInformation("Policy found: {PolicyNumber} for CustomerId: {CustomerId}", 
                policy.PolicyNumber, policy.CustomerId);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPolicy for PolicyNumber: {PolicyNumber}", policyNumber);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<PolicyDto[]>> GetPoliciesByCustomer(int customerId)
    {
        try
        {
            _logger.LogInformation("GetPoliciesByCustomer called for CustomerId: {CustomerId}", customerId);

            var policies = await _context.Policies
                .Where(p => p.CustomerId == customerId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("Found {Count} policies for CustomerId: {CustomerId}", policies.Count, customerId);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPoliciesByCustomer for CustomerId: {CustomerId}", customerId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<PolicyDto>> CreatePolicy([FromBody] CreatePolicyRequest request)
    {
        try
        {
            _logger.LogInformation("CreatePolicy called for CustomerId: {CustomerId}", request.CustomerId);

            var customer = await _context.Customers.FindAsync(request.CustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
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

            _logger.LogInformation("Policy created: {PolicyNumber} for CustomerId: {CustomerId}", 
                policy.PolicyNumber, request.CustomerId);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreatePolicy for CustomerId: {CustomerId}", request.CustomerId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
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

