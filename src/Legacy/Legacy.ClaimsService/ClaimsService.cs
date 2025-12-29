using System.ServiceModel;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;
using SeguroAuto.Domain;

namespace Legacy.ClaimsService;

public class ClaimsService : IClaimsService
{
    private readonly SeguroAutoDbContext _context;
    private readonly ILogger<ClaimsService> _logger;

    public ClaimsService(SeguroAutoDbContext context, ILogger<ClaimsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public ClaimResponse GetClaim(GetClaimRequest request)
    {
        try
        {
            _logger.LogInformation("GetClaim called for ClaimNumber: {ClaimNumber}", request.ClaimNumber);

            var claim = _context.Claims
                .Include(c => c.Policy)
                .FirstOrDefault(c => c.ClaimNumber == request.ClaimNumber);

            if (claim == null)
            {
                _logger.LogWarning("Claim not found: {ClaimNumber}", request.ClaimNumber);
                throw new FaultException("Claim not found");
            }

            _logger.LogInformation("Claim found: {ClaimNumber} for PolicyNumber: {PolicyNumber}", 
                claim.ClaimNumber, claim.Policy.PolicyNumber);

            return new ClaimResponse
            {
                ClaimNumber = claim.ClaimNumber,
                PolicyNumber = claim.Policy.PolicyNumber,
                Description = claim.Description,
                Amount = claim.Amount,
                Status = claim.Status.ToString(),
                IncidentDate = claim.IncidentDate
            };
        }
        catch (FaultException)
        {
            throw; // Re-throw FaultException as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClaim for ClaimNumber: {ClaimNumber}", request.ClaimNumber);
            throw new FaultException($"Error retrieving claim: {ex.Message}");
        }
    }

    public GetClaimsByPolicyResponse GetClaimsByPolicy(GetClaimsByPolicyRequest request)
    {
        try
        {
            _logger.LogInformation("GetClaimsByPolicy called for PolicyNumber: {PolicyNumber}", request.PolicyNumber);

            var policy = _context.Policies.FirstOrDefault(p => p.PolicyNumber == request.PolicyNumber);
            if (policy == null)
            {
                _logger.LogWarning("Policy not found: {PolicyNumber}", request.PolicyNumber);
                return new GetClaimsByPolicyResponse { Claims = Array.Empty<ClaimResponse>() };
            }

            var claims = _context.Claims
                .Where(c => c.PolicyId == policy.Id)
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            _logger.LogInformation("Found {Count} claims for PolicyNumber: {PolicyNumber}", 
                claims.Count, request.PolicyNumber);

            return new GetClaimsByPolicyResponse
            {
                Claims = claims.Select(c => new ClaimResponse
                {
                    ClaimNumber = c.ClaimNumber,
                    PolicyNumber = policy.PolicyNumber,
                    Description = c.Description,
                    Amount = c.Amount,
                    Status = c.Status.ToString(),
                    IncidentDate = c.IncidentDate
                }).ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetClaimsByPolicy for PolicyNumber: {PolicyNumber}", request.PolicyNumber);
            throw new FaultException($"Error retrieving claims: {ex.Message}");
        }
    }

    public ClaimResponse CreateClaim(CreateClaimRequest request)
    {
        try
        {
            _logger.LogInformation("CreateClaim called for PolicyNumber: {PolicyNumber}", request.PolicyNumber);

            var policy = _context.Policies.FirstOrDefault(p => p.PolicyNumber == request.PolicyNumber);
            if (policy == null)
            {
                _logger.LogWarning("Policy not found: {PolicyNumber}", request.PolicyNumber);
                throw new FaultException("Policy not found");
            }

            var claim = new Claim
            {
                ClaimNumber = $"CLAIM-{DateTime.UtcNow:yyyyMMddHHmmss}",
                PolicyId = policy.Id,
                Description = request.Description,
                Amount = request.Amount,
                Status = ClaimStatus.Pending,
                IncidentDate = request.IncidentDate,
                CreatedAt = DateTime.UtcNow
            };

            _context.Claims.Add(claim);
            _context.SaveChanges();

            _logger.LogInformation("Claim created: {ClaimNumber} for PolicyNumber: {PolicyNumber}", 
                claim.ClaimNumber, request.PolicyNumber);

            return new ClaimResponse
            {
                ClaimNumber = claim.ClaimNumber,
                PolicyNumber = policy.PolicyNumber,
                Description = claim.Description,
                Amount = claim.Amount,
                Status = claim.Status.ToString(),
                IncidentDate = claim.IncidentDate
            };
        }
        catch (FaultException)
        {
            throw; // Re-throw FaultException as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateClaim for PolicyNumber: {PolicyNumber}", request.PolicyNumber);
            throw new FaultException($"Error creating claim: {ex.Message}");
        }
    }
}

