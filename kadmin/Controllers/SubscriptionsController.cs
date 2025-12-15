using kadmin.Models;
using kadmin.Services;
using Microsoft.AspNetCore.Mvc;

namespace kadmin.Controllers;

public class SubscriptionsController : Controller
{
    private readonly AdminDataService _data;

    public SubscriptionsController(AdminDataService data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public async Task<IActionResult> Index(Guid? userId, CancellationToken cancellationToken)
    {
        var subscriptions = await _data.GetSubscriptionsAsync(userId, cancellationToken);
        ViewData["UserId"] = userId;
        return View(subscriptions);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken cancellationToken)
    {
        SubscriptionEditModel model;
        if (id.HasValue)
        {
            var subscription = await _data.GetSubscriptionAsync(id.Value, cancellationToken);
            if (subscription == null)
            {
                return NotFound();
            }

            model = new SubscriptionEditModel
            {
                Id = subscription.Id,
                UserId = subscription.UserId,
                PlanId = subscription.PlanId,
                Status = subscription.Status,
                CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
                CurrentPeriodStart = subscription.CurrentPeriodStart,
                CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                StripeSubscriptionId = subscription.StripeSubscriptionId,
                StripeCustomerId = subscription.StripeCustomerId
            };
        }
        else
        {
            model = new SubscriptionEditModel();
        }

        ViewBag.Plans = await _data.GetPlansAsync(cancellationToken);
        ViewBag.Users = await _data.GetUsersForLookupAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SubscriptionEditModel model, CancellationToken cancellationToken)
    {
        if (model.UserId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.UserId), "Pick a user");
        }

        if (model.PlanId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.PlanId), "Pick a plan");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Plans = await _data.GetPlansAsync(cancellationToken);
            ViewBag.Users = await _data.GetUsersForLookupAsync(cancellationToken);
            return View(model);
        }

        var id = await _data.SaveSubscriptionAsync(model, cancellationToken);
        if (!id.HasValue)
        {
            return NotFound();
        }

        TempData["Message"] = "Subscription saved.";
        return RedirectToAction(nameof(Index));
    }
}
