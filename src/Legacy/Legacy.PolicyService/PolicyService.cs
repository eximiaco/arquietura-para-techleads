using System.ServiceModel;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;
using SeguroAuto.Domain;

namespace Legacy.PolicyService;

public class PolicyService : IPolicyService
{
    private readonly SeguroAutoDbContext _context;
    private readonly ILogger<PolicyService> _logger;

    public PolicyService(SeguroAutoDbContext context, ILogger<PolicyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public PolicyResponse GetPolicy(GetPolicyRequest request)
    {
        _logger.LogInformation("GetPolicy called for PolicyNumber: {PolicyNumber}", request.PolicyNumber);

        var policy = _context.Policies
            .Include(p => p.Customer)
            .FirstOrDefault(p => p.PolicyNumber == request.PolicyNumber);

        if (policy == null)
        {
            throw new FaultException("Policy not found");
        }

        return new PolicyResponse
        {
            PolicyNumber = policy.PolicyNumber,
            CustomerId = policy.CustomerId,
            VehiclePlate = policy.VehiclePlate,
            Premium = policy.Premium,
            StartDate = policy.StartDate,
            EndDate = policy.EndDate,
            Status = policy.Status.ToString()
        };
    }

    public GetPoliciesByCustomerResponse GetPoliciesByCustomer(GetPoliciesByCustomerRequest request)
    {
        _logger.LogInformation("GetPoliciesByCustomer called for CustomerId: {CustomerId}", request.CustomerId);

        var policies = _context.Policies
            .Where(p => p.CustomerId == request.CustomerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        return new GetPoliciesByCustomerResponse
        {
            Policies = policies.Select(p => new PolicyResponse
            {
                PolicyNumber = p.PolicyNumber,
                CustomerId = p.CustomerId,
                VehiclePlate = p.VehiclePlate,
                Premium = p.Premium,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Status = p.Status.ToString()
            }).ToArray()
        };
    }

    public PolicyResponse CreatePolicy(CreatePolicyRequest request)
    {
        _logger.LogInformation("CreatePolicy called for CustomerId: {CustomerId}", request.CustomerId);

        var customer = _context.Customers.Find(request.CustomerId);
        if (customer == null)
        {
            throw new FaultException("Customer not found");
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
        _context.SaveChanges();

        return new PolicyResponse
        {
            PolicyNumber = policy.PolicyNumber,
            CustomerId = policy.CustomerId,
            VehiclePlate = policy.VehiclePlate,
            Premium = policy.Premium,
            StartDate = policy.StartDate,
            EndDate = policy.EndDate,
            Status = policy.Status.ToString()
        };
    }
}

