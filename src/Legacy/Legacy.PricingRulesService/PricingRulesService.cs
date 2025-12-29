using System.ServiceModel;
using Microsoft.EntityFrameworkCore;
using SeguroAuto.Data;

namespace Legacy.PricingRulesService;

public class PricingRulesService : IPricingRulesService
{
    private readonly SeguroAutoDbContext _context;
    private readonly ILogger<PricingRulesService> _logger;

    public PricingRulesService(SeguroAutoDbContext context, ILogger<PricingRulesService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public GetAllRulesResponse GetAllRules(GetAllRulesRequest request)
    {
        try
        {
            _logger.LogInformation("GetAllRules called");

            var rules = _context.PricingRules
                .OrderBy(r => r.Id)
                .ToList();

            _logger.LogInformation("Found {Count} pricing rules", rules.Count);

            return new GetAllRulesResponse
            {
                Rules = rules.Select(r => new PricingRuleResponse
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    Multiplier = r.Multiplier,
                    IsActive = r.IsActive
                }).ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAllRules");
            throw new FaultException($"Error retrieving pricing rules: {ex.Message}");
        }
    }

    public PricingRuleResponse GetRule(GetRuleRequest request)
    {
        try
        {
            _logger.LogInformation("GetRule called for RuleId: {RuleId}", request.RuleId);

            var rule = _context.PricingRules.Find(request.RuleId);
            if (rule == null)
            {
                _logger.LogWarning("Rule not found: {RuleId}", request.RuleId);
                throw new FaultException("Rule not found");
            }

            _logger.LogInformation("Rule found: {RuleId} - {Name}", rule.Id, rule.Name);

            return new PricingRuleResponse
            {
                Id = rule.Id,
                Name = rule.Name,
                Description = rule.Description,
                Multiplier = rule.Multiplier,
                IsActive = rule.IsActive
            };
        }
        catch (FaultException)
        {
            throw; // Re-throw FaultException as-is
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRule for RuleId: {RuleId}", request.RuleId);
            throw new FaultException($"Error retrieving rule: {ex.Message}");
        }
    }

    public UpdateRuleResponse UpdateRule(UpdateRuleRequest request)
    {
        try
        {
            _logger.LogInformation("UpdateRule called for RuleId: {RuleId}, IsActive: {IsActive}", 
                request.Id, request.IsActive);

            var rule = _context.PricingRules.Find(request.Id);
            if (rule == null)
            {
                _logger.LogWarning("Rule not found: {RuleId}", request.Id);
                return new UpdateRuleResponse { Success = false };
            }

            rule.IsActive = request.IsActive;
            _context.SaveChanges();

            _logger.LogInformation("Rule updated: {RuleId} - IsActive: {IsActive}", request.Id, request.IsActive);

            return new UpdateRuleResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateRule for RuleId: {RuleId}", request.Id);
            throw new FaultException($"Error updating rule: {ex.Message}");
        }
    }
}

