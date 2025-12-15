using kadmin.Services;
using Microsoft.AspNetCore.Mvc;

namespace kadmin.Controllers;

public class DashboardController : Controller
{
    private readonly AdminDataService _data;

    public DashboardController(AdminDataService data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _data.GetDashboardAsync(cancellationToken);
        return View(model);
    }
}
