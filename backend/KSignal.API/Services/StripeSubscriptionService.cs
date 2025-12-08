using System.Text.Json;
using KSignal.API.Data;
using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.BillingPortal;

namespace KSignal.API.Services;

public class StripeSubscriptionService
{
    private readonly KalshiDbContext _db;
    private readonly StripeOptions _options;
    private readonly ILogger<StripeSubscriptionService> _logger;

    public StripeSubscriptionService(
        KalshiDbContext db,
        IOptions<StripeOptions> options,
        ILogger<StripeSubscriptionService> logger)
    {
        _db = db;
        _logger = logger;
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            StripeConfiguration.ApiKey = _options.SecretKey;
        }
    }

    public async Task<List<SubscriptionPlan>> GetActivePlansAsync(CancellationToken cancellationToken)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);
        var changed = await SeedPlansAsync(cancellationToken);
        if (changed)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Amount)
            .ToListAsync(cancellationToken);
    }

    public async Task<PaymentLink> CreatePaymentLinkAsync(
        User user,
        SubscriptionPlan plan,
        string successUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Stripe secret key is not configured.");
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);

        // Pre-create or retrieve Stripe customer with user information
        var customerId = await EnsureCustomerAsync(user, cancellationToken);

        var metadata = new Dictionary<string, string>
        {
            { "userId", user.Id.ToString() },
            { "firebaseId", user.FirebaseId },
            { "planCode", plan.Code },
            { "planId", plan.Id }
        };

        // Add user email and username to metadata if available
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            metadata["userEmail"] = user.Email;
        }
        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            metadata["username"] = user.Username;
        }

        var paymentLinkOptions = new PaymentLinkCreateOptions
        {
            LineItems = new List<PaymentLinkLineItemOptions>
            {
                new PaymentLinkLineItemOptions
                {
                    Price = plan.StripePriceId,
                    Quantity = 1
                }
            },
            AfterCompletion = new PaymentLinkAfterCompletionOptions
            {
                Type = "redirect",
                Redirect = new PaymentLinkAfterCompletionRedirectOptions
                {
                    Url = successUrl
                }
            },
            Metadata = metadata,
            // Use existing customer instead of creating new one
            CustomerCreation = string.IsNullOrWhiteSpace(customerId) ? "always" : "if_required",
            SubscriptionData = new PaymentLinkSubscriptionDataOptions
            {
                Metadata = metadata,
                Description = $"{plan.Name} subscription for {user.Email ?? user.Username ?? user.FirebaseId}"
            },
            InvoiceCreation = new PaymentLinkInvoiceCreationOptions
            {
                Enabled = true,
                InvoiceData = new PaymentLinkInvoiceCreationInvoiceDataOptions
                {
                    Description = $"{plan.Name} - {user.Email ?? user.Username ?? user.FirebaseId}",
                    Metadata = metadata
                }
            }
        };

        // If we have a customer ID, specify it
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            // For payment links, we can't set customer directly, but CustomerCreation will link it
            _logger.LogInformation("Creating payment link for existing customer {CustomerId} (User {UserId})", customerId, user.Id);
        }

        var paymentLinkService = new PaymentLinkService();
        var paymentLink = await paymentLinkService.CreateAsync(paymentLinkOptions, requestOptions: null, cancellationToken);

        var activeSubscription = await EnsureSubscriptionShellAsync(user, plan, cancellationToken);
        await LogEventAsync(user, activeSubscription.Id, "payment_link_created", $"Payment link {paymentLink.Id} for plan {plan.Code}", JsonSerializer.Serialize(paymentLink), cancellationToken);

        return paymentLink;
    }

    public async Task<Session> CreatePortalSessionAsync(User user, string returnUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Stripe secret key is not configured.");
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        var customerId = await EnsureCustomerAsync(user, cancellationToken);

        var portalService = new SessionService();
        var session = await portalService.CreateAsync(new SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        }, cancellationToken: cancellationToken);

        await LogEventAsync(user, user.ActiveSubscriptionId, "billing_portal", $"Billing portal session {session.Id} created", null, cancellationToken);
        return session;
    }

    public async Task<(bool HasActiveSubscription, SubscriptionPlan? Plan, UserSubscription? Subscription)> GetUserSubscriptionStatusAsync(
        User user,
        CancellationToken cancellationToken)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);

        SubscriptionPlan? plan = null;
        UserSubscription? subscription = null;

        // Load subscription plan if user has one
        if (!string.IsNullOrWhiteSpace(user.ActivePlanId))
        {
            plan = await _db.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == user.ActivePlanId, cancellationToken);
        }

        // Load subscription details
        if (!string.IsNullOrWhiteSpace(user.ActiveSubscriptionId))
        {
            subscription = await _db.UserSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == user.ActiveSubscriptionId, cancellationToken);
        }

        // User has active subscription if status is active and subscription period hasn't ended
        var hasActiveSubscription = !string.IsNullOrWhiteSpace(user.SubscriptionStatus) &&
            user.SubscriptionStatus.Equals("active", StringComparison.OrdinalIgnoreCase) &&
            subscription?.CurrentPeriodEnd > DateTime.UtcNow;

        return (hasActiveSubscription, plan, subscription);
    }

    public async Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken)
    {
        Event? stripeEvent;
        try
        {
            stripeEvent = !string.IsNullOrWhiteSpace(_options.WebhookSecret)
                ? EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret)
                : EventUtility.ParseEvent(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Stripe webhook signature");
            throw;
        }

        switch (stripeEvent.Type)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                var subscription = stripeEvent.Data.Object as Subscription;
                if (subscription != null)
                {
                    await UpsertSubscriptionAsync(subscription, stripeEvent.Type, cancellationToken);
                }
                break;

            case "checkout.session.completed":
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session != null)
                {
                    await HandleCheckoutCompletedAsync(session, cancellationToken);
                }
                break;

            case "payment_link.payment_succeeded":
                // Handle payment link completion
                var paymentLinkPayment = stripeEvent.Data.Object as PaymentLink;
                if (paymentLinkPayment != null)
                {
                    _logger.LogInformation("Payment link {PaymentLinkId} payment succeeded", paymentLinkPayment.Id);
                    // The subscription will be handled by customer.subscription.created event
                    // This event confirms payment was successful
                }
                break;

            case "payment_intent.succeeded":
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                if (paymentIntent != null)
                {
                    await HandlePaymentIntentAsync(paymentIntent, stripeEvent.Type, cancellationToken);
                }
                break;

            case "invoice.payment_succeeded":
                var invoice = stripeEvent.Data.Object as Invoice;
                // In Clover API, subscription is accessed via Parent.SubscriptionDetails.Subscription
                var invoiceSubscription = invoice?.Parent?.SubscriptionDetails?.Subscription;
                if (invoiceSubscription?.Id != null)
                {
                    var subscriptionService = new SubscriptionService();
                    var paidSubscription = await subscriptionService.GetAsync(invoiceSubscription.Id, cancellationToken: cancellationToken);
                    await UpsertSubscriptionAsync(paidSubscription, stripeEvent.Type, cancellationToken);
                }
                break;

            default:
                _logger.LogDebug("Unhandled Stripe event type: {Type}", stripeEvent.Type);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(Stripe.Checkout.Session session, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.SubscriptionId))
        {
            _logger.LogWarning("Checkout completed without subscription id. Session {SessionId}", session.Id);
            return;
        }

        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(session.SubscriptionId, cancellationToken: cancellationToken);
        await UpsertSubscriptionAsync(subscription, "checkout.session.completed", cancellationToken);
    }

    private async Task HandlePaymentIntentAsync(PaymentIntent intent, string eventType, CancellationToken cancellationToken)
    {
        var user = await ResolveUserAsync(intent.CustomerId, intent.Metadata, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("Payment intent {PaymentIntentId} processed without matching user (customer {CustomerId})", intent.Id, intent.CustomerId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(intent.CustomerId) && string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            user.StripeCustomerId = intent.CustomerId;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        await LogEventAsync(
            user,
            user.ActiveSubscriptionId,
            eventType,
            $"Payment intent {intent.Id} succeeded",
            JsonSerializer.Serialize(new
            {
                intent.Id,
                intent.AmountReceived,
                intent.Currency
            }),
            cancellationToken);
    }

    private async Task UpsertSubscriptionAsync(Subscription subscription, string eventType, CancellationToken cancellationToken)
    {
        var customerId = subscription.CustomerId;

        var user = await ResolveUserAsync(customerId, subscription.Metadata, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("No user found for Stripe subscription {SubId} (customer {CustomerId})", subscription.Id, customerId);
            return;
        }

        var planPriceId = subscription.Items?.Data.FirstOrDefault()?.Price?.Id;
        var plan = !string.IsNullOrWhiteSpace(planPriceId)
            ? await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.StripePriceId == planPriceId, cancellationToken)
            : null;

        var currentSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription.Id || s.Id == user.ActiveSubscriptionId, cancellationToken);

        if (currentSub == null)
        {
            currentSub = new UserSubscription
            {
                Id = user.ActiveSubscriptionId ?? Guid.NewGuid().ToString("N"),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow
            };
            _db.UserSubscriptions.Add(currentSub);
        }

        currentSub.StripeSubscriptionId = subscription.Id;
        currentSub.StripeCustomerId = customerId;
        currentSub.PlanId = plan?.Id ?? currentSub.PlanId ?? planPriceId ?? string.Empty;
        currentSub.Status = subscription.Status ?? "unknown";
        currentSub.CancelAtPeriodEnd = subscription.CancelAtPeriodEnd;

        // In Clover API, current period dates are on subscription items, not the subscription itself
        var firstItem = subscription.Items?.Data?.FirstOrDefault();
        if (firstItem != null)
        {
            currentSub.CurrentPeriodStart = firstItem.CurrentPeriodStart != default
                ? firstItem.CurrentPeriodStart
                : currentSub.CurrentPeriodStart;
            currentSub.CurrentPeriodEnd = firstItem.CurrentPeriodEnd != default
                ? firstItem.CurrentPeriodEnd
                : currentSub.CurrentPeriodEnd;
        }

        currentSub.UpdatedAt = DateTime.UtcNow;

        user.ActiveSubscriptionId = currentSub.Id;
        user.ActivePlanId = currentSub.PlanId;
        user.SubscriptionStatus = currentSub.Status;
        user.StripeCustomerId = customerId;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await LogEventAsync(
            user,
            currentSub.Id,
            eventType,
            $"Subscription {subscription.Id} updated via webhook",
            JsonSerializer.Serialize(new
            {
                subscription.Id,
                subscription.Status,
                PlanCode = plan?.Code,
                PlanId = plan?.Id,
                PriceId = planPriceId
            }),
            cancellationToken);
    }

    private async Task<UserSubscription> EnsureSubscriptionShellAsync(User user, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        var existing = !string.IsNullOrWhiteSpace(user.ActiveSubscriptionId)
            ? await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.Id == user.ActiveSubscriptionId, cancellationToken)
            : null;

        if (existing == null)
        {
            existing = new UserSubscription
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = user.Id,
                PlanId = plan.Id,
                Status = "pending_payment",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.UserSubscriptions.Add(existing);
        }
        else
        {
            existing.PlanId = plan.Id;
            existing.Status = "pending_payment";
            existing.UpdatedAt = DateTime.UtcNow;
        }

        user.ActiveSubscriptionId = existing.Id;
        user.ActivePlanId = plan.Id;
        user.SubscriptionStatus = existing.Status;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private async Task<string> EnsureCustomerAsync(User user, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            return user.StripeCustomerId;
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("Stripe secret key is not configured.");
        }

        var customerMetadata = new Dictionary<string, string>
        {
            { "firebaseId", user.FirebaseId },
            { "userId", user.Id.ToString() }
        };

        // Add optional user information to metadata
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            customerMetadata["userEmail"] = user.Email;
        }
        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            customerMetadata["username"] = user.Username;
        }

        var customerService = new CustomerService();
        var created = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = user.Email,
            Name = user.Username ?? user.Email ?? user.FirebaseId,
            Description = $"User ID: {user.Id}, Firebase ID: {user.FirebaseId}",
            Metadata = customerMetadata
        }, cancellationToken: cancellationToken);

        user.StripeCustomerId = created.Id;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await LogEventAsync(user, null, "customer_created", $"Created Stripe customer {created.Id}", null, cancellationToken);
        return created.Id;
    }

    private async Task<User?> ResolveUserAsync(string? customerId, IDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            var byCustomer = await _db.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == customerId, cancellationToken);
            if (byCustomer != null)
            {
                return byCustomer;
            }
        }

        if (metadata != null && metadata.Count > 0)
        {
            if (metadata.TryGetValue("userId", out var userIdRaw) && ulong.TryParse(userIdRaw, out var userId))
            {
                var byId = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (byId != null)
                {
                    return byId;
                }
            }

            if (metadata.TryGetValue("firebaseId", out var firebaseId) && !string.IsNullOrWhiteSpace(firebaseId))
            {
                var byFirebase = await _db.Users.FirstOrDefaultAsync(u => u.FirebaseId == firebaseId, cancellationToken);
                if (byFirebase != null)
                {
                    return byFirebase;
                }
            }
        }

        return null;
    }

    private async Task<bool> SeedPlansAsync(CancellationToken cancellationToken)
    {
        var changed = false;
        var seedPlans = new List<SubscriptionPlan>();

        if (!string.IsNullOrWhiteSpace(_options.CoreDataPriceId))
        {
            seedPlans.Add(new SubscriptionPlan
            {
                Id = "core-data",
                Code = "core-data",
                Name = "Core Data",
                StripePriceId = _options.CoreDataPriceId,
                Currency = "usd",
                Interval = "month",
                Amount = 79,
                Description = "Streamlined market data feed with history and export support."
            });
        }

        if (!string.IsNullOrWhiteSpace(_options.CoreDataAnnualPriceId))
        {
            seedPlans.Add(new SubscriptionPlan
            {
                Id = "core-data-annual",
                Code = "core-data-annual",
                Name = "Core Data",
                StripePriceId = _options.CoreDataAnnualPriceId,
                Currency = "usd",
                Interval = "year",
                Amount = 790,
                Description = "Annual billing for Core Data."
            });
        }

        foreach (var seed in seedPlans)
        {
            var existing = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == seed.Id, cancellationToken);
            if (existing == null)
            {
                _db.SubscriptionPlans.Add(seed);
                changed = true;
            }
            else
            {
                var needsUpdate =
                    existing.StripePriceId != seed.StripePriceId ||
                    existing.Name != seed.Name ||
                    existing.Code != seed.Code ||
                    existing.Amount != seed.Amount ||
                    existing.Interval != seed.Interval ||
                    existing.Description != seed.Description;

                if (needsUpdate)
                {
                    existing.StripePriceId = seed.StripePriceId;
                    existing.Name = seed.Name;
                    existing.Code = seed.Code;
                    existing.Amount = seed.Amount;
                    existing.Interval = seed.Interval;
                    existing.Description = seed.Description;
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private async Task LogEventAsync(User user, string? subscriptionId, string eventType, string? notes, string? data, CancellationToken cancellationToken)
    {
        _db.SubscriptionEvents.Add(new SubscriptionEventLog
        {
            UserId = user.Id,
            SubscriptionId = subscriptionId,
            EventType = eventType,
            Notes = notes,
            Data = data,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
