using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using web_asp.Services;
using KSignals.DTO;

namespace web_asp.Pages;

public class PricingModel : PageModel
{
    private readonly BackendClient _backendClient;
    private readonly ILogger<PricingModel> _logger;
    private readonly string _coreDataPaymentLink;

    public List<SubscriptionPlanDto> Plans { get; private set; } = new();
    public SubscriptionSummaryResponse? CurrentSubscription { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? StatusMessage { get; private set; }
    public string CoreDataPaymentLink => _coreDataPaymentLink;

    public PricingModel(BackendClient backendClient, ILogger<PricingModel> logger, IConfiguration configuration)
    {
        _backendClient = backendClient;
        _logger = logger;
        var configuredLink = configuration["payment_link_core_data"];
        _coreDataPaymentLink = string.IsNullOrWhiteSpace(configuredLink)
            ? "https://buy.stripe.com/test_5kQ00kbN8fqjeqPdfwafS00"
            : configuredLink;
    }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();

        if (Request.Query.TryGetValue("canceled", out _))
        {
            StatusMessage = "Checkout canceled. No changes were made.";
        }

        if (Request.Query.TryGetValue("checkout", out var checkout) && checkout.ToString().Equals("success", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Subscription updated. Welcome aboard.";
        }
    }

    public async Task<IActionResult> OnPostCheckoutAsync(string planCode)
    {
        if (string.IsNullOrWhiteSpace(planCode))
        {
            ErrorMessage = "Choose a plan to continue.";
            await LoadDataAsync();
            return Page();
        }

        if (!Request.Cookies.TryGetValue("ksignals_jwt", out var jwt) || string.IsNullOrWhiteSpace(jwt))
        {
            var returnUrl = Url.Page("/Pricing");
            return RedirectToPage("/Login", new { returnUrl });
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}/subscription/success";
        var cancelUrl = $"{baseUrl}/pricing?canceled=true&plan={Uri.EscapeDataString(planCode)}";

        var (success, error, checkout) = await _backendClient.CreateCheckoutSessionAsync(jwt, planCode, successUrl, cancelUrl);
        if (!success || checkout == null)
        {
            ErrorMessage = error ?? "Unable to start payment link. Please try again.";
            await LoadDataAsync();
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(checkout.Url))
        {
            return Redirect(checkout.Url);
        }

        StatusMessage = "Payment link created. Follow the Stripe flow to finish.";
        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPortalAsync()
    {
        if (!Request.Cookies.TryGetValue("ksignals_jwt", out var jwt) || string.IsNullOrWhiteSpace(jwt))
        {
            var returnUrl = Url.Page("/Pricing");
            return RedirectToPage("/Login", new { returnUrl });
        }

        var returnUrlParam = $"{Request.Scheme}://{Request.Host}/pricing";
        var (success, error, url) = await _backendClient.CreatePortalSessionAsync(jwt, returnUrlParam);

        if (success && !string.IsNullOrWhiteSpace(url))
        {
            return Redirect(url);
        }

        ErrorMessage = error ?? "Unable to open billing portal right now.";
        await LoadDataAsync();
        return Page();
    }

    private async Task LoadDataAsync()
    {
        var fetchedPlans = (await _backendClient.GetSubscriptionPlansAsync()).ToList();
        var relevantPlans = fetchedPlans
            .Where(p =>
                string.Equals(p.Code, "free", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Code, "core-data", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var plansByCode = FallbackPlans()
            .ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var plan in relevantPlans)
        {
            plansByCode[plan.Code] = plan;
        }

        Plans = plansByCode.Values
            .OrderBy(p => string.Equals(p.Code, "free", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();

        if (Request.Cookies.TryGetValue("ksignals_jwt", out var jwt) && !string.IsNullOrWhiteSpace(jwt))
        {
            var (success, error, summary) = await _backendClient.GetSubscriptionAsync(jwt);
            if (success)
            {
                CurrentSubscription = summary;
            }
            else
            {
                _logger.LogWarning("Unable to load subscription status: {Error}", error);
            }
        }
    }

    private static List<SubscriptionPlanDto> FallbackPlans() => new()
    {
        new SubscriptionPlanDto
        {
            Id = "free",
            Code = "free",
            Name = "Free",
            Amount = 0,
            Currency = "usd",
            Interval = "month",
            Description = "Try Kalshi Signals with free market coverage."
        },
        new SubscriptionPlanDto
        {
            Id = "core-data",
            Code = "core-data",
            Name = "Core Data",
            Amount = 79,
            Currency = "usd",
            Interval = "month",
            Description = "Live and historical market data feed with export-ready access."
        }
    };
}
