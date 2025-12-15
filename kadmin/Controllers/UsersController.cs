using kadmin.Models;
using kadmin.Services;
using Microsoft.AspNetCore.Mvc;

namespace kadmin.Controllers;

public class UsersController : Controller
{
    private readonly AdminDataService _data;

    public UsersController(AdminDataService data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var users = await _data.GetUsersAsync(q, cancellationToken);
        ViewData["Search"] = q;
        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken cancellationToken)
    {
        UserDetailViewModel? vm;
        if (id.HasValue)
        {
            vm = await _data.GetUserDetailAsync(id.Value, cancellationToken);
            if (vm == null)
            {
                return NotFound();
            }
        }
        else
        {
            vm = new UserDetailViewModel
            {
                Plans = await _data.GetPlansAsync(cancellationToken)
            };
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var fallback = form.Id.HasValue
                ? await _data.GetUserDetailAsync(form.Id.Value, cancellationToken)
                : new UserDetailViewModel();

            if (fallback == null)
            {
                return NotFound();
            }

            fallback.Form = form;
            fallback.Plans = await _data.GetPlansAsync(cancellationToken);
            return View(fallback);
        }

        var id = await _data.SaveUserAsync(form, cancellationToken);
        if (!id.HasValue)
        {
            return NotFound();
        }

        TempData["Message"] = "User saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }
}
