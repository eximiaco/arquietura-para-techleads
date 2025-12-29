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
        _logger.LogInformation("GetAllRules called");

        var rules = _context.PricingRules
            .OrderBy(r => r.Id)
            .ToList();

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

    public PricingRuleResponse GetRule(GetRuleRequest request)
    {
        _logger.LogInformation("GetRule called for RuleId: {RuleId}", request.RuleId);

        var rule = _context.PricingRules.Find(request.RuleId);
        if (rule == null)
        {
            throw new FaultException("Rule not found");
        }

        return new PricingRuleResponse
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            Multiplier = rule.Multiplier,
            IsActive = rule.IsActive
        };
    }

    public UpdateRuleResponse UpdateRule(UpdateRuleRequest request)
    {
        _logger.LogInformation("UpdateRule called for RuleId: {RuleId}, IsActive: {IsActive}", 
            request.Id, request.IsActive);

        var rule = _context.PricingRules.Find(request.Id);
        if (rule == null)
        {
            return new UpdateRuleResponse { Success = false };
        }

        rule.IsActive = request.IsActive;
        _context.SaveChanges();

        return new UpdateRuleResponse { Success = true };
    }
}

