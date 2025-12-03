using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using web_asp.Services;
using KSignals.DTO;

namespace web_asp.Pages.Markets;

public class DetailsModel : PageModel
{
    private readonly BackendClient _backendClient;

    public DetailsModel(BackendClient backendClient)
    {
        _backendClient = backendClient;
    }

    public ClientEventDetailsResponse? EventDetails { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? EventTicker)
    {
        if (string.IsNullOrWhiteSpace(EventTicker))
        {
            return NotFound();
        }

        EventDetails = await _backendClient.GetEventDetailsAsync(EventTicker);
        if (EventDetails == null)
        {
            return NotFound();
        }

        return Page();
    }
}
