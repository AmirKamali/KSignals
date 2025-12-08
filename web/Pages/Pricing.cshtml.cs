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
    private readonly string _coreDataAnnualPaymentLink;

    public List<SubscriptionTierDto> Tiers { get; private set; } = new();
    public List<SubscriptionPlanDto> Plans { get; private set; } = new();
    public SubscriptionSummaryResponse? CurrentSubscription { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? StatusMessage { get; private set; }
    public string CoreDataPaymentLink => _coreDataPaymentLink;
    public string CoreDataAnnualPaymentLink => _coreDataAnnualPaymentLink;
    public string SelectedCadence { get; private set; } = "monthly";

    public PricingModel(BackendClient backendClient, ILogger<PricingModel> logger, IConfiguration configuration)
    {
        _backendClient = backendClient;
        _logger = logger;
        var configuredLink = configuration["payment_link_core_data"];
        _coreDataPaymentLink = string.IsNullOrWhiteSpace(configuredLink)
            ? "https://buy.stripe.com/test_5kQ00kbN8fqjeqPdfwafS00"
            : configuredLink;

        var configuredAnnualLink = configuration["payment_link_core_data_annual"];
        _coreDataAnnualPaymentLink = string.IsNullOrWhiteSpace(configuredAnnualLink)
            ? _coreDataPaymentLink
            : configuredAnnualLink;
    }

    public async Task OnGetAsync()
    {
        SelectedCadence = "monthly";
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

    public async Task<IActionResult> OnPostCheckoutAsync(string planCode, string? cadence)
    {
        SelectedCadence = NormalizeCadence(cadence);

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
        var fetchedTiers = (await _backendClient.GetSubscriptionTierPricingAsync()).ToList();
        Tiers = MergeWithFallbackTiers(fetchedTiers);
        Plans = Tiers
            .SelectMany(t => new[] { t.MonthlyPlan, t.AnnualPlan })
            .Where(p => p != null)
            .Cast<SubscriptionPlanDto>()
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

    private static List<SubscriptionTierDto> MergeWithFallbackTiers(List<SubscriptionTierDto> fetchedTiers)
    {
        var fallbackTiers = FallbackTiers()
            .ToDictionary(t => t.Tier, StringComparer.OrdinalIgnoreCase);

        foreach (var tier in fetchedTiers)
        {
            if (!fallbackTiers.TryGetValue(tier.Tier, out var existing))
            {
                fallbackTiers[tier.Tier] = tier;
                continue;
            }

            existing.Name = string.IsNullOrWhiteSpace(tier.Name) ? existing.Name : tier.Name;
            existing.MonthlyPlan = tier.MonthlyPlan ?? existing.MonthlyPlan;
            existing.AnnualPlan = tier.AnnualPlan ?? existing.AnnualPlan;
        }

        return fallbackTiers.Values
            .OrderBy(t => t.MonthlyPlan?.Amount ?? t.AnnualPlan?.Amount ?? decimal.MaxValue)
            .ToList();
    }

    private static List<SubscriptionTierDto> FallbackTiers() => new()
    {
        new SubscriptionTierDto
        {
            Tier = "free",
            Name = "Free",
            MonthlyPlan = new SubscriptionPlanDto
            {
                Id = "free",
                Code = "free",
                Name = "Free",
                Amount = 0,
                Currency = "usd",
                Interval = "month",
                Description = "Try Kalshi Signals with free market coverage."
            }
        },
        new SubscriptionTierDto
        {
            Tier = "core-data",
            Name = "Core Data",
            MonthlyPlan = new SubscriptionPlanDto
            {
                Id = "core-data",
                Code = "core-data",
                Name = "Core Data",
                Amount = 79,
                Currency = "usd",
                Interval = "month",
                Description = "Live and historical market data feed with export-ready access."
            },
            AnnualPlan = new SubscriptionPlanDto
            {
                Id = "core-data-annual",
                Code = "core-data-annual",
                Name = "Core Data",
                Amount = 790,
                Currency = "usd",
                Interval = "year",
                Description = "Annual billing for Core Data with export-ready access."
            }
        }
    };

    private static string NormalizeCadence(string? cadence)
    {
        return cadence != null && cadence.Equals("annual", StringComparison.OrdinalIgnoreCase)
            ? "annual"
            : "monthly";
    }
}
