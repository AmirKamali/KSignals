using KSignal.API.Data;
using KSignal.API.Models;
using KSignal.API.Services;
using KSignals.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace KSignal.API.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Produces("application/json")]
public class SubscriptionsController : ControllerBase
{
    private readonly KalshiDbContext _db;
    private readonly StripeSubscriptionService _stripe;
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(
        KalshiDbContext db,
        StripeSubscriptionService stripe,
        IOptions<StripeOptions> stripeOptions,
        ILogger<SubscriptionsController> logger)
    {
        _db = db;
        _stripe = stripe;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    [HttpGet("plans")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await _stripe.GetActivePlansAsync(cancellationToken);
        return Ok(new
        {
            plans = plans.Select(MapPlan)
        });
    }

    [HttpGet("tiers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTierPricing(CancellationToken cancellationToken)
    {
        var activePlans = await _stripe.GetActivePlansAsync(cancellationToken);
        var tiers = BuildTiers(activePlans);

        return Ok(new
        {
            tiers
        });
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySubscription(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user == null)
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        var (plan, periodEnd) = await LoadSubscriptionAsync(user, cancellationToken);

        var events = await _db.SubscriptionEvents.AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var response = new SubscriptionSummaryResponse
        {
            ActivePlan = plan != null ? MapPlan(plan) : null,
            Status = user.SubscriptionStatus,
            CurrentPeriodEnd = periodEnd,
            StripeCustomerId = user.StripeCustomerId,
            Events = events.Select(e => new SubscriptionEventDto
            {
                EventType = e.EventType,
                Notes = e.Notes,
                CreatedAt = e.CreatedAt
            }).ToList()
        };

        return Ok(response);
    }

    [Authorize]
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSubscriptionStatus(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user == null)
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        var (hasActiveSubscription, plan, subscription) = await _stripe.GetUserSubscriptionStatusAsync(user, cancellationToken);

        return Ok(new
        {
            hasActiveSubscription,
            isUpgraded = hasActiveSubscription,
            plan = plan != null ? MapPlan(plan) : null,
            status = user.SubscriptionStatus,
            currentPeriodEnd = subscription?.CurrentPeriodEnd,
            currentPeriodStart = subscription?.CurrentPeriodStart,
            cancelAtPeriodEnd = subscription?.CancelAtPeriodEnd ?? false,
            stripeCustomerId = user.StripeCustomerId
        });
    }

    [Authorize]
    [HttpPost("checkout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.PlanCode))
        {
            return BadRequest(new { error = "planCode is required" });
        }

        var user = await GetUserAsync(cancellationToken);
        if (user == null)
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        var plans = await _stripe.GetActivePlansAsync(cancellationToken);
        var plan = plans.FirstOrDefault(p => p.Code.Equals(request.PlanCode, StringComparison.OrdinalIgnoreCase));

        if (plan == null)
        {
            return BadRequest(new { error = "Unknown plan" });
        }

        if (string.IsNullOrWhiteSpace(plan.StripePriceId))
        {
            return BadRequest(new { error = "Plan is not configured for checkout" });
        }

        var successUrl = !string.IsNullOrWhiteSpace(request.SuccessUrl)
            ? request.SuccessUrl
            : (!string.IsNullOrWhiteSpace(_stripeOptions.SuccessUrl)
                ? _stripeOptions.SuccessUrl
                : $"{Request.Scheme}://{Request.Host}/user/account");

        var paymentLink = await _stripe.CreatePaymentLinkAsync(user, plan, successUrl, cancellationToken);

        return Ok(new CreateCheckoutResponse
        {
            SessionId = paymentLink.Id,
            PaymentLinkId = paymentLink.Id,
            PublicKey = _stripeOptions.PublishableKey,
            Url = paymentLink.Url
        });
    }

    [Authorize]
    [HttpPost("portal")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreatePortalSession(
        [FromBody] CreatePortalSessionRequest? request,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user == null)
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        var returnUrl = !string.IsNullOrWhiteSpace(request?.ReturnUrl)
            ? request!.ReturnUrl!
            : _stripeOptions.BillingPortalReturnUrl ?? _stripeOptions.SuccessUrl;

        var session = await _stripe.CreatePortalSessionAsync(user, returnUrl, cancellationToken);
        return Ok(new { url = session.Url });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(HttpContext.Request.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"];

        try
        {
            await _stripe.HandleWebhookAsync(json, signature!, cancellationToken);
            return Ok(new { received = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe webhook handling failed");
            return BadRequest(new { error = "Webhook validation failed" });
        }
    }

    private SubscriptionPlanDto MapPlan(SubscriptionPlan plan) => new()
    {
        Id = plan.Id.ToString(),
        Code = plan.Code,
        Name = plan.Name,
        Amount = plan.Amount,
        Currency = plan.Currency,
        Interval = plan.Interval,
        Description = plan.Description,
        IsActive = plan.IsActive
    };

    private List<SubscriptionTierDto> BuildTiers(IEnumerable<SubscriptionPlan> activePlans)
    {
        var plansByCode = FallbackPlans()
            .ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var plan in activePlans)
        {
            plansByCode[plan.Code] = plan;
        }

        var tiers = new Dictionary<string, SubscriptionTierDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var plan in plansByCode.Values)
        {
            var tierCode = NormalizeTierCode(plan.Code);
            if (!tiers.TryGetValue(tierCode, out var tier))
            {
                tier = new SubscriptionTierDto
                {
                    Tier = tierCode,
                    Name = plan.Name
                };
                tiers[tierCode] = tier;
            }

            var mapped = MapPlan(plan);
            if (IsAnnualPlan(plan))
            {
                tier.AnnualPlan = mapped;
                if (string.IsNullOrWhiteSpace(tier.Name))
                {
                    tier.Name = plan.Name;
                }
            }
            else
            {
                tier.MonthlyPlan = mapped;
                tier.Name = plan.Name;
            }
        }

        return tiers.Values
            .OrderBy(t => t.MonthlyPlan?.Amount ?? t.AnnualPlan?.Amount ?? decimal.MaxValue)
            .ToList();
    }

    private List<SubscriptionPlan> FallbackPlans() => new()
    {
        new SubscriptionPlan
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Code = "free",
            Name = "Free",
            StripePriceId = string.Empty,
            Currency = "usd",
            Interval = "month",
            Amount = 0,
            Description = "Try Kalshi Signals with free market coverage.",
            IsActive = true
        },
        new SubscriptionPlan
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Code = "core-data",
            Name = "Core Data",
            StripePriceId = _stripeOptions.CoreDataPriceId ?? string.Empty,
            Currency = "usd",
            Interval = "month",
            Amount = 79,
            Description = "Streamlined market data feed with history and export support.",
            IsActive = true
        },
        new SubscriptionPlan
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Code = "core-data-annual",
            Name = "Core Data",
            StripePriceId = _stripeOptions.CoreDataAnnualPriceId ?? string.Empty,
            Currency = "usd",
            Interval = "year",
            Amount = 790,
            Description = "Annual billing for Core Data.",
            IsActive = true
        }
    };

    private static bool IsAnnualPlan(SubscriptionPlan plan)
    {
        return plan.Code.EndsWith("-annual", StringComparison.OrdinalIgnoreCase) ||
               plan.Interval.Equals("year", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTierCode(string planCode)
    {
        return planCode.EndsWith("-annual", StringComparison.OrdinalIgnoreCase)
            ? planCode[..^"-annual".Length]
            : planCode;
    }

    private async Task<User?> GetUserAsync(CancellationToken cancellationToken)
    {
        var firebaseId = GetFirebaseIdFromClaims();
        if (string.IsNullOrWhiteSpace(firebaseId))
        {
            return null;
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        return await _db.Users.FirstOrDefaultAsync(u => u.FirebaseId == firebaseId, cancellationToken);
    }

    private async Task<(SubscriptionPlan? Plan, DateTime? PeriodEnd)> LoadSubscriptionAsync(User user, CancellationToken cancellationToken)
    {
        SubscriptionPlan? plan = null;
        if (user.ActivePlanId.HasValue)
        {
            plan = await _db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == user.ActivePlanId.Value, cancellationToken);
        }

        DateTime? periodEnd = null;
        if (user.ActiveSubscriptionId.HasValue)
        {
            var subscription = await _db.UserSubscriptions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == user.ActiveSubscriptionId.Value, cancellationToken);
            periodEnd = subscription?.CurrentPeriodEnd;
        }

        return (plan, periodEnd);
    }

    private string? GetFirebaseIdFromClaims()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
    }
}
