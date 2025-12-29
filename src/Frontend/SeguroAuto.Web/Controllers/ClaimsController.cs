using Microsoft.AspNetCore.Mvc;
using SeguroAuto.Web.Models;
using SeguroAuto.Web.Services;

namespace SeguroAuto.Web.Controllers;

public class ClaimsController : Controller
{
    private readonly IClaimsServiceClient _claimsServiceClient;
    private readonly ILogger<ClaimsController> _logger;

    public ClaimsController(
        IClaimsServiceClient claimsServiceClient,
        ILogger<ClaimsController> logger)
    {
        _claimsServiceClient = claimsServiceClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string policyNumber)
    {
        try
        {
            if (string.IsNullOrEmpty(policyNumber))
            {
                TempData["Error"] = "Número da apólice é obrigatório.";
                return View(new List<ClaimViewModel>());
            }

            var claims = await _claimsServiceClient.GetClaimsByPolicyAsync(policyNumber);
            
            var viewModels = claims.Select(c => new ClaimViewModel
            {
                ClaimNumber = c.ClaimNumber,
                PolicyNumber = c.PolicyNumber,
                Description = c.Description,
                Amount = c.Amount,
                Status = c.Status,
                IncidentDate = c.IncidentDate
            }).ToList();

            ViewBag.PolicyNumber = policyNumber;
            return View(viewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading claims for PolicyNumber: {PolicyNumber}", policyNumber);
            TempData["Error"] = "Erro ao carregar sinistros. Tente novamente.";
            return View(new List<ClaimViewModel>());
        }
    }

    [HttpGet]
    public IActionResult Create(string policyNumber)
    {
        if (string.IsNullOrEmpty(policyNumber))
        {
            TempData["Error"] = "Número da apólice é obrigatório.";
            return RedirectToAction(nameof(Index));
        }

        return View(new ClaimCreateViewModel 
        { 
            PolicyNumber = policyNumber,
            IncidentDate = DateTime.Now
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClaimCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var claim = await _claimsServiceClient.CreateClaimAsync(
                model.PolicyNumber,
                model.Description,
                model.Amount,
                model.IncidentDate);

            TempData["Success"] = $"Sinistro {claim.ClaimNumber} criado com sucesso!";
            return RedirectToAction(nameof(Index), new { policyNumber = model.PolicyNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating claim");
            ModelState.AddModelError("", "Erro ao criar sinistro. Tente novamente.");
            return View(model);
        }
    }
}

