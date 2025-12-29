using Microsoft.AspNetCore.Mvc;
using SeguroAuto.Web.Models;
using SeguroAuto.Web.Services;

namespace SeguroAuto.Web.Controllers;

public class QuotesController : Controller
{
    private readonly IQuoteServiceClient _quoteServiceClient;
    private readonly ILogger<QuotesController> _logger;

    public QuotesController(
        IQuoteServiceClient quoteServiceClient,
        ILogger<QuotesController> logger)
    {
        _quoteServiceClient = quoteServiceClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index(int? customerId)
    {
        try
        {
            var customer = customerId ?? 999;
            var quotes = await _quoteServiceClient.GetQuotesByCustomerAsync(customer);
            
            var viewModels = quotes.Select(q => new QuoteViewModel
            {
                QuoteNumber = q.QuoteNumber,
                CustomerId = q.CustomerId,
                Premium = q.Premium,
                ValidUntil = q.ValidUntil,
                Status = q.Status
            }).ToList();

            ViewBag.CustomerId = customer;
            return View(viewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading quotes for CustomerId: {CustomerId}", customerId);
            TempData["Error"] = "Erro ao carregar cotações. Tente novamente.";
            return View(new List<QuoteViewModel>());
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new QuoteCreateViewModel { CustomerId = 999 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuoteCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var quote = await _quoteServiceClient.GetQuoteAsync(
                model.CustomerId,
                model.VehiclePlate,
                model.VehicleModel,
                model.VehicleYear);

            TempData["Success"] = $"Cotação {quote.QuoteNumber} criada com sucesso!";
            return RedirectToAction(nameof(Index), new { customerId = model.CustomerId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quote");
            ModelState.AddModelError("", "Erro ao criar cotação. Tente novamente.");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string quoteNumber, int? customerId)
    {
        try
        {
            var success = await _quoteServiceClient.ApproveQuoteAsync(quoteNumber);
            
            if (success)
            {
                TempData["Success"] = $"Cotação {quoteNumber} aprovada com sucesso!";
            }
            else
            {
                TempData["Error"] = $"Não foi possível aprovar a cotação {quoteNumber}.";
            }

            return RedirectToAction(nameof(Index), new { customerId = customerId ?? 999 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving quote: {QuoteNumber}", quoteNumber);
            TempData["Error"] = "Erro ao aprovar cotação. Tente novamente.";
            return RedirectToAction(nameof(Index), new { customerId = customerId ?? 999 });
        }
    }
}

