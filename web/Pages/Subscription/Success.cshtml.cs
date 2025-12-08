using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using web_asp.Services;
using KSignals.DTO;

namespace web_asp.Pages.Subscription;

public class SuccessModel : PageModel
{
    private readonly BackendClient _backendClient;
    private readonly ILogger<SuccessModel> _logger;

    public bool IsProcessing { get; private set; }
    public bool IsActivated { get; private set; }
    public bool HasTimeout { get; private set; }
    public string? ErrorMessage { get; private set; }
    public SubscriptionStatusResponse? SubscriptionStatus { get; private set; }

    private int _checkAttempts;
    private const int MaxCheckAttempts = 20;

    public SuccessModel(BackendClient backendClient, ILogger<SuccessModel> logger)
    {
        _backendClient = backendClient;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Check if user is authenticated
        if (!Request.Cookies.TryGetValue("ksignals_jwt", out var jwt) || string.IsNullOrWhiteSpace(jwt))
        {
            return RedirectToPage("/Login", new { returnUrl = "/subscription/success" });
        }

        // Check if we've exceeded max attempts (timeout)
        if (Request.Query.TryGetValue("attempts", out var attemptsStr) && int.TryParse(attemptsStr, out var attempts))
        {
            _checkAttempts = attempts;
        }

        if (_checkAttempts >= MaxCheckAttempts)
        {
            HasTimeout = true;
            return Page();
        }

        // Check subscription status
        var (success, error, status) = await _backendClient.GetSubscriptionStatusAsync(jwt);

        if (!success)
        {
            _logger.LogWarning("Failed to check subscription status: {Error}", error);
            ErrorMessage = error ?? "Unable to check subscription status. Please try again.";
            return Page();
        }

        if (status?.HasActiveSubscription == true)
        {
            // Subscription is activated!
            IsActivated = true;
            SubscriptionStatus = status;
            return Page();
        }

        // Still processing - show processing state
        IsProcessing = true;
        return Page();
    }

    public async Task<IActionResult> OnGetCheckStatusAsync()
    {
        // AJAX handler for status checks
        if (!Request.Cookies.TryGetValue("ksignals_jwt", out var jwt) || string.IsNullOrWhiteSpace(jwt))
        {
            return new JsonResult(new { isActivated = false, error = "Not authenticated" });
        }

        var (success, error, status) = await _backendClient.GetSubscriptionStatusAsync(jwt);

        if (!success)
        {
            return new JsonResult(new { isActivated = false, error });
        }

        return new JsonResult(new
        {
            isActivated = status?.HasActiveSubscription == true,
            status = status?.Status,
            planName = status?.Plan?.Name
        });
    }
}
