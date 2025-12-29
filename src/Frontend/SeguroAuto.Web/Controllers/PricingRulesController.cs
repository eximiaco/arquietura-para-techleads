using Microsoft.AspNetCore.Mvc;
using SeguroAuto.Web.Models;
using SeguroAuto.Web.Services;

namespace SeguroAuto.Web.Controllers;

public class PricingRulesController : Controller
{
    private readonly IPricingRulesServiceClient _pricingRulesServiceClient;
    private readonly ILogger<PricingRulesController> _logger;

    public PricingRulesController(
        IPricingRulesServiceClient pricingRulesServiceClient,
        ILogger<PricingRulesController> logger)
    {
        _pricingRulesServiceClient = pricingRulesServiceClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var rules = await _pricingRulesServiceClient.GetAllRulesAsync();
            
            var viewModels = rules.Select(r => new PricingRuleViewModel
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Multiplier = r.Multiplier,
                IsActive = r.IsActive
            }).ToList();

            return View(viewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pricing rules");
            TempData["Error"] = "Erro ao carregar regras de precificação. Tente novamente.";
            return View(new List<PricingRuleViewModel>());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, bool isActive)
    {
        try
        {
            var success = await _pricingRulesServiceClient.UpdateRuleAsync(id, !isActive);
            
            if (success)
            {
                TempData["Success"] = $"Regra {(isActive ? "desativada" : "ativada")} com sucesso!";
            }
            else
            {
                TempData["Error"] = "Não foi possível atualizar a regra.";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling rule: {Id}", id);
            TempData["Error"] = "Erro ao atualizar regra. Tente novamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}

