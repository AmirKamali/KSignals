using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using web_asp.Models;
using web_asp.Services;

namespace web_asp.Pages.Markets;

public class DetailsModel : PageModel
{
    private readonly BackendClient _backendClient;

    public DetailsModel(BackendClient backendClient)
    {
        _backendClient = backendClient;
    }

    public MarketDetails? Market { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? tickerId)
    {
        if (string.IsNullOrWhiteSpace(tickerId))
        {
            return NotFound();
        }

        Market = await _backendClient.GetMarketDetailsAsync(tickerId);
        if (Market == null)
        {
            return NotFound();
        }

        return Page();
    }
}
