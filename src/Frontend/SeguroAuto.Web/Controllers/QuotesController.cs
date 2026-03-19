using System.Diagnostics;
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
            var correlationId = Activity.Current?.TraceId.ToString() ?? "";
            _logger.LogError(ex, "Error loading quotes for CustomerId: {CustomerId}, CorrelationId: {CorrelationId}",
                customerId, correlationId);
            TempData["Error"] = $"Erro ao carregar cotações. CorrelationId: {correlationId}";
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
    public async Task<IActionResult> Create(QuoteCreateViewModel model, bool simulateError = false)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var correlationId = Activity.Current?.TraceId.ToString() ?? "";

        try
        {
            var quote = await _quoteServiceClient.GetQuoteAsync(
                model.CustomerId,
                model.VehiclePlate,
                model.VehicleModel,
                model.VehicleYear,
                simulateError);

            TempData["Success"] = $"Cotação {quote.QuoteNumber} criada com sucesso!";
            return RedirectToAction(nameof(Index), new { customerId = model.CustomerId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quote. SimulateError: {SimulateError}, CorrelationId: {CorrelationId}",
                simulateError, correlationId);
            var errorMsg = simulateError
                ? $"Erro simulado na procedure do banco ao criar cotação. CorrelationId: {correlationId}"
                : $"Erro ao criar cotação. CorrelationId: {correlationId}";
            ModelState.AddModelError("", errorMsg);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string quoteNumber, int? customerId, bool simulateError = false)
    {
        // Captura o TraceId no início — antes de qualquer chamada que possa falhar
        var correlationId = Activity.Current?.TraceId.ToString() ?? "";

        try
        {
            var success = await _quoteServiceClient.ApproveQuoteAsync(quoteNumber, simulateError);

            if (success)
            {
                TempData["Success"] = $"Cotação {quoteNumber} aprovada com sucesso!";
            }
            else
            {
                TempData["Error"] = $"Não foi possível aprovar a cotação {quoteNumber}. CorrelationId: {correlationId}";
            }

            return RedirectToAction(nameof(Index), new { customerId = customerId ?? 999 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving quote: {QuoteNumber}, SimulateError: {SimulateError}, CorrelationId: {CorrelationId}",
                quoteNumber, simulateError, correlationId);
            TempData["Error"] = simulateError
                ? $"Erro simulado na aprovação da cotação {quoteNumber}. CorrelationId: {correlationId}"
                : $"Erro ao aprovar cotação. CorrelationId: {correlationId}";
            return RedirectToAction(nameof(Index), new { customerId = customerId ?? 999 });
        }
    }
}

