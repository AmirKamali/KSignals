using kadmin.Models;
using kadmin.Services;
using Microsoft.AspNetCore.Mvc;

namespace kadmin.Controllers;

public class EventsController : Controller
{
    private readonly AdminDataService _data;

    public EventsController(AdminDataService data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var events = await _data.GetEventsAsync(cancellationToken);
        return View(events);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? userId, Guid? subscriptionId, CancellationToken cancellationToken)
    {
        var model = new EventCreateModel
        {
            UserId = userId ?? Guid.Empty,
            SubscriptionId = subscriptionId
        };

        ViewBag.Users = await _data.GetUsersForLookupAsync(cancellationToken);
        ViewBag.Subscriptions = await _data.GetSubscriptionsAsync(userId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventCreateModel model, CancellationToken cancellationToken)
    {
        if (model.UserId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.UserId), "Choose a user");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Users = await _data.GetUsersForLookupAsync(cancellationToken);
            ViewBag.Subscriptions = await _data.GetSubscriptionsAsync(model.UserId == Guid.Empty ? null : model.UserId, cancellationToken);
            return View(model);
        }

        var ok = await _data.AddEventAsync(model, cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "User not found. Please pick a valid user.");
            ViewBag.Users = await _data.GetUsersForLookupAsync(cancellationToken);
            ViewBag.Subscriptions = await _data.GetSubscriptionsAsync(model.UserId == Guid.Empty ? null : model.UserId, cancellationToken);
            return View(model);
        }

        TempData["Message"] = "Event recorded.";
        return RedirectToAction(nameof(Index));
    }
}
