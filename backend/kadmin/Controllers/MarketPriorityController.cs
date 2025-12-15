using kadmin.Models;
using kadmin.Services;
using Microsoft.AspNetCore.Mvc;

namespace kadmin.Controllers;

public class MarketPriorityController : Controller
{
    private readonly AdminDataService _data;

    public MarketPriorityController(AdminDataService data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _data.GetMarketHighPrioritiesAsync(cancellationToken);
        var model = new MarketPriorityPageModel
        {
            Items = items,
            Form = new MarketPriorityForm
            {
                Priority = items.FirstOrDefault()?.Priority + 1 ?? 1
            }
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MarketPriorityForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var items = await _data.GetMarketHighPrioritiesAsync(cancellationToken);
            return View("Index", new MarketPriorityPageModel { Items = items, Form = form });
        }

        await _data.UpsertMarketHighPriorityAsync(form, cancellationToken);
        TempData["Message"] = "Priority saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string tickerId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(tickerId))
        {
            await _data.DeleteMarketHighPriorityAsync(tickerId, cancellationToken);
            TempData["Message"] = $"Removed {tickerId} from high priority.";
        }

        return RedirectToAction(nameof(Index));
    }
}
