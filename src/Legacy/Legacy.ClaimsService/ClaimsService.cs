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
        _logger.LogInformation("GetClaim called for ClaimNumber: {ClaimNumber}", request.ClaimNumber);

        var claim = _context.Claims
            .Include(c => c.Policy)
            .FirstOrDefault(c => c.ClaimNumber == request.ClaimNumber);

        if (claim == null)
        {
            throw new FaultException("Claim not found");
        }

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

    public GetClaimsByPolicyResponse GetClaimsByPolicy(GetClaimsByPolicyRequest request)
    {
        _logger.LogInformation("GetClaimsByPolicy called for PolicyNumber: {PolicyNumber}", request.PolicyNumber);

        var policy = _context.Policies.FirstOrDefault(p => p.PolicyNumber == request.PolicyNumber);
        if (policy == null)
        {
            return new GetClaimsByPolicyResponse { Claims = Array.Empty<ClaimResponse>() };
        }

        var claims = _context.Claims
            .Where(c => c.PolicyId == policy.Id)
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

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

    public ClaimResponse CreateClaim(CreateClaimRequest request)
    {
        _logger.LogInformation("CreateClaim called for PolicyNumber: {PolicyNumber}", request.PolicyNumber);

        var policy = _context.Policies.FirstOrDefault(p => p.PolicyNumber == request.PolicyNumber);
        if (policy == null)
        {
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
}

