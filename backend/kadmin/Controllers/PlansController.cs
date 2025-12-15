using kadmin.Models;
using kadmin.Services;
using Microsoft.AspNetCore.Mvc;

namespace kadmin.Controllers;

public class PlansController : Controller
{
    private readonly AdminDataService _data;

    public PlansController(AdminDataService data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var plans = await _data.GetPlansAsync(cancellationToken);
        return View(plans);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken cancellationToken)
    {
        PlanEditModel model;
        if (id.HasValue)
        {
            var plan = await _data.GetPlanAsync(id.Value, cancellationToken);
            if (plan == null)
            {
                return NotFound();
            }

            model = new PlanEditModel
            {
                Id = plan.Id,
                Code = plan.Code,
                Name = plan.Name,
                StripePriceId = plan.StripePriceId,
                Currency = plan.Currency,
                Interval = plan.Interval,
                Amount = plan.Amount,
                IsActive = plan.IsActive,
                Description = plan.Description
            };
        }
        else
        {
            model = new PlanEditModel();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PlanEditModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var id = await _data.SavePlanAsync(model, cancellationToken);
        TempData["Message"] = "Plan saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FetchPrice(PlanEditModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        var (amount, currency, interval, error) = await _data.RefreshPlanFromStripeAsync(model, cancellationToken);
        if (!string.IsNullOrWhiteSpace(error) || amount == null)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to fetch price.");
            TempData["Message"] = error ?? "Unable to fetch price.";
            TempData["MessageType"] = "error";
            return View("Edit", model);
        }

        model.Amount = amount.Value;
        if (!string.IsNullOrWhiteSpace(currency))
        {
            model.Currency = currency;
        }

        if (!string.IsNullOrWhiteSpace(interval))
        {
            model.Interval = interval;
        }

        await _data.SavePlanAsync(model, cancellationToken);
        TempData["Message"] = $"Fetched price {amount:0.00} {model.Currency}/{model.Interval} from Stripe.";
        TempData["MessageType"] = "info";
        return RedirectToAction(nameof(Edit), new { id = model.Id });
    }
}
