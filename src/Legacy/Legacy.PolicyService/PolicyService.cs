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
        try
        {
            _logger.LogInformation("GetPolicy called for PolicyNumber: {PolicyNumber}", request.PolicyNumber);

            var policy = _context.Policies
                .Include(p => p.Customer)
                .FirstOrDefault(p => p.PolicyNumber == request.PolicyNumber);

            if (policy == null)
            {
                _logger.LogWarning("Policy not found: {PolicyNumber}", request.PolicyNumber);
                throw new FaultException("Policy not found");
            }

            _logger.LogInformation("Policy found: {PolicyNumber} for CustomerId: {CustomerId}", 
                policy.PolicyNumber, policy.CustomerId);

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
        catch (FaultException)
        {
            throw; // Re-throw FaultException as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPolicy for PolicyNumber: {PolicyNumber}", request.PolicyNumber);
            throw new FaultException($"Error retrieving policy: {ex.Message}");
        }
    }

    public GetPoliciesByCustomerResponse GetPoliciesByCustomer(GetPoliciesByCustomerRequest request)
    {
        try
        {
            _logger.LogInformation("GetPoliciesByCustomer called for CustomerId: {CustomerId}", request.CustomerId);

            var policies = _context.Policies
                .Where(p => p.CustomerId == request.CustomerId)
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            _logger.LogInformation("Found {Count} policies for CustomerId: {CustomerId}", policies.Count, request.CustomerId);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPoliciesByCustomer for CustomerId: {CustomerId}", request.CustomerId);
            throw new FaultException($"Error retrieving policies: {ex.Message}");
        }
    }

    public PolicyResponse CreatePolicy(CreatePolicyRequest request)
    {
        try
        {
            _logger.LogInformation("CreatePolicy called for CustomerId: {CustomerId}", request.CustomerId);

            var customer = _context.Customers.Find(request.CustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Customer not found: {CustomerId}", request.CustomerId);
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

            _logger.LogInformation("Policy created: {PolicyNumber} for CustomerId: {CustomerId}", 
                policy.PolicyNumber, request.CustomerId);

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
        catch (FaultException)
        {
            throw; // Re-throw FaultException as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreatePolicy for CustomerId: {CustomerId}", request.CustomerId);
            throw new FaultException($"Error creating policy: {ex.Message}");
        }
    }
}

