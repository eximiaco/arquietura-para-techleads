using Microsoft.AspNetCore.Mvc;
using SeguroAuto.Web.Models;
using SeguroAuto.Web.Services;

namespace SeguroAuto.Web.Controllers;

public class PoliciesController : Controller
{
    private readonly IPolicyServiceClient _policyServiceClient;
    private readonly ILogger<PoliciesController> _logger;

    public PoliciesController(
        IPolicyServiceClient policyServiceClient,
        ILogger<PoliciesController> logger)
    {
        _policyServiceClient = policyServiceClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index(int? customerId)
    {
        try
        {
            var customer = customerId ?? 999;
            var policies = await _policyServiceClient.GetPoliciesByCustomerAsync(customer);
            
            var viewModels = policies.Select(p => new PolicyViewModel
            {
                PolicyNumber = p.PolicyNumber,
                CustomerId = p.CustomerId,
                VehiclePlate = p.VehiclePlate,
                Premium = p.Premium,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Status = p.Status
            }).ToList();

            ViewBag.CustomerId = customer;
            return View(viewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading policies for CustomerId: {CustomerId}", customerId);
            TempData["Error"] = "Erro ao carregar apólices. Tente novamente.";
            return View(new List<PolicyViewModel>());
        }
    }

    public async Task<IActionResult> Details(string policyNumber)
    {
        try
        {
            var policy = await _policyServiceClient.GetPolicyAsync(policyNumber);
            
            var viewModel = new PolicyViewModel
            {
                PolicyNumber = policy.PolicyNumber,
                CustomerId = policy.CustomerId,
                VehiclePlate = policy.VehiclePlate,
                Premium = policy.Premium,
                StartDate = policy.StartDate,
                EndDate = policy.EndDate,
                Status = policy.Status
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading policy: {PolicyNumber}", policyNumber);
            TempData["Error"] = "Erro ao carregar apólice. Tente novamente.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new PolicyCreateViewModel { CustomerId = 999 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PolicyCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var policy = await _policyServiceClient.CreatePolicyAsync(
                model.CustomerId,
                model.VehiclePlate,
                model.VehicleModel,
                model.VehicleYear,
                model.Premium);

            TempData["Success"] = $"Apólice {policy.PolicyNumber} criada com sucesso!";
            return RedirectToAction(nameof(Details), new { policyNumber = policy.PolicyNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating policy");
            ModelState.AddModelError("", "Erro ao criar apólice. Tente novamente.");
            return View(model);
        }
    }
}

