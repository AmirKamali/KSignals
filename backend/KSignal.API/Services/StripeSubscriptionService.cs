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
            { "planId", plan.Id.ToString() }
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
            SubscriptionData = new PaymentLinkSubscriptionDataOptions
            {
                Metadata = metadata,
                Description = $"{plan.Name} subscription for {user.Email ?? user.Username ?? user.FirebaseId}"
            }
        };

        // If we have a customer ID, specify it
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            // For payment links, we can't set customer directly, but CustomerCreation will link it
            _logger.LogInformation("Creating payment link for existing customer {CustomerId} (FirebaseId {FirebaseId})", customerId, user.FirebaseId);
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
        if (user.ActivePlanId.HasValue)
        {
            plan = await _db.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == user.ActivePlanId.Value, cancellationToken);
        }

        // Load subscription details
        if (user.ActiveSubscriptionId.HasValue)
        {
            subscription = await _db.UserSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == user.ActiveSubscriptionId.Value, cancellationToken);
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
                var invoiceSubscriptionId =
                    invoice?.Parent?.SubscriptionDetails?.Subscription?.Id ??
                    invoice?.Parent?.SubscriptionDetails?.SubscriptionId;

                if (!string.IsNullOrWhiteSpace(invoiceSubscriptionId))
                {
                    var subscriptionService = new SubscriptionService();
                    var paidSubscription = await subscriptionService.GetAsync(invoiceSubscriptionId, cancellationToken: cancellationToken);
                    await UpsertSubscriptionAsync(paidSubscription, stripeEvent.Type, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("invoice.payment_succeeded without subscription id. Invoice {InvoiceId}", invoice?.Id);
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
        var metadataFirebaseId = intent.Metadata.TryGetValue("firebaseId", out var firebaseId)
            ? firebaseId
            : null;

        // Fallback: resolve via charge metadata/email if payment intent metadata/customer didn't match
        if (user == null && !string.IsNullOrWhiteSpace(intent.LatestChargeId))
        {
            var chargeService = new ChargeService();
            var charge = await chargeService.GetAsync(intent.LatestChargeId, cancellationToken: cancellationToken);

            user = await ResolveUserAsync(charge.CustomerId, charge.Metadata, cancellationToken);

            if (user == null && !string.IsNullOrWhiteSpace(charge.BillingDetails?.Email))
            {
                user = await _db.Users.FirstOrDefaultAsync(u => u.Email == charge.BillingDetails.Email, cancellationToken);
            }

            if (user != null && string.IsNullOrWhiteSpace(user.StripeCustomerId) && !string.IsNullOrWhiteSpace(charge.CustomerId))
            {
                user.StripeCustomerId = charge.CustomerId;
                user.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        if (user == null)
        {
            _logger.LogWarning(
                "Payment intent {PaymentIntentId} processed without matching user (customer {CustomerId}, charge {ChargeId}, firebaseId {FirebaseId})",
                intent.Id,
                intent.CustomerId,
                intent.LatestChargeId,
                metadataFirebaseId ?? "(none)");
            return;
        }

        if (!string.IsNullOrWhiteSpace(metadataFirebaseId) &&
            !string.Equals(metadataFirebaseId, user.FirebaseId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Payment intent {PaymentIntentId} metadata FirebaseId {MetadataFirebaseId} does not match user (FirebaseId {UserFirebaseId})",
                intent.Id,
                metadataFirebaseId,
                user.FirebaseId);
        }

        if (!string.IsNullOrWhiteSpace(intent.CustomerId) && string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            user.StripeCustomerId = intent.CustomerId;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        await SyncSubscriptionFromPaymentIntentAsync(intent, user, eventType, cancellationToken);

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

    private async Task SyncSubscriptionFromPaymentIntentAsync(
        PaymentIntent intent,
        User user,
        string eventType,
        CancellationToken cancellationToken)
    {
        var invoiceId = intent.Metadata.TryGetValue("invoiceId", out var metaInvoiceId)
            ? metaInvoiceId
            : null;
        var subscriptionId = intent.Metadata.TryGetValue("subscriptionId", out var metaSubId)
            ? metaSubId
            : null;

        var planId = intent.Metadata.TryGetValue("planId", out var metadataPlanId) ? metadataPlanId : null;
        var planCode = intent.Metadata.TryGetValue("planCode", out var metadataPlanCode) ? metadataPlanCode : null;

        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            var subscriptionService = new SubscriptionService();
            var subscription = await subscriptionService.GetAsync(subscriptionId, cancellationToken: cancellationToken);
            if (subscription != null)
            {
                await UpsertSubscriptionAsync(subscription, eventType, cancellationToken);
                return;
            }
        }

        SubscriptionPlan? plan = null;
        if (!string.IsNullOrWhiteSpace(planId) && Guid.TryParse(planId, out var parsedPlanId))
        {
            plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == parsedPlanId, cancellationToken);
        }

        if (plan == null && !string.IsNullOrWhiteSpace(planCode))
        {
            plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == planCode, cancellationToken);
        }

        if (plan != null)
        {
            var subscriptionShell = await EnsureSubscriptionShellAsync(user, plan, cancellationToken);
            subscriptionShell.Status = "active";
            subscriptionShell.UpdatedAt = DateTime.UtcNow;

            user.SubscriptionStatus = subscriptionShell.Status;
            user.ActivePlanId = plan.Id;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await LogEventAsync(
                user,
                subscriptionShell.Id,
                eventType,
                $"Payment intent {intent.Id} activated plan {plan.Code} without Stripe subscription reference",
                null,
                cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Payment intent {PaymentIntentId} succeeded but no subscription could be resolved (invoice {InvoiceId})",
                intent.Id,
                invoiceId ?? "(none)");
        }
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
            .FirstOrDefaultAsync(s =>
                s.StripeSubscriptionId == subscription.Id ||
                s.Id == user.ActiveSubscriptionId ||
                s.UserId == user.Id,
                cancellationToken);

        if (currentSub == null)
        {
            currentSub = new UserSubscription
            {
                Id = Guid.NewGuid(), // Generate UUID client-side to avoid RETURNING clause
                UserId = user.Id,
                Status = "pending", // Set explicitly to avoid RETURNING clause
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow // Set explicitly to avoid RETURNING clause
            };
            _db.UserSubscriptions.Add(currentSub);
        }

        currentSub.StripeSubscriptionId = subscription.Id;
        currentSub.StripeCustomerId = customerId;
        if (plan != null)
        {
            currentSub.PlanId = plan.Id;
        }
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
        var existing = user.ActiveSubscriptionId.HasValue
            ? await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.Id == user.ActiveSubscriptionId.Value, cancellationToken)
            : await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == user.Id, cancellationToken);

        if (existing == null)
        {
            existing = new UserSubscription
            {
                Id = Guid.NewGuid(), // Generate UUID client-side to avoid RETURNING clause
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
            if (metadata.TryGetValue("userId", out var userIdRaw) && Guid.TryParse(userIdRaw, out var userId))
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

        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(_options.CoreDataPriceId))
        {
            seedPlans.Add(new SubscriptionPlan
            {
                Id = Guid.NewGuid(), // Generate UUID client-side to avoid RETURNING clause
                Code = "core-data",
                Name = "Core Data",
                StripePriceId = _options.CoreDataPriceId,
                Currency = "usd",
                Interval = "month",
                Amount = 79,
                IsActive = true, // Set explicitly to avoid RETURNING clause
                Description = "Streamlined market data feed with history and export support.",
                CreatedAt = now, // Set explicitly to avoid RETURNING clause
                UpdatedAt = now // Set explicitly to avoid RETURNING clause
            });
        }

        if (!string.IsNullOrWhiteSpace(_options.CoreDataAnnualPriceId))
        {
            seedPlans.Add(new SubscriptionPlan
            {
                Id = Guid.NewGuid(), // Generate UUID client-side to avoid RETURNING clause
                Code = "core-data-annual",
                Name = "Core Data",
                StripePriceId = _options.CoreDataAnnualPriceId,
                Currency = "usd",
                Interval = "year",
                Amount = 790,
                IsActive = true, // Set explicitly to avoid RETURNING clause
                Description = "Annual billing for Core Data.",
                CreatedAt = now, // Set explicitly to avoid RETURNING clause
                UpdatedAt = now // Set explicitly to avoid RETURNING clause
            });
        }

        foreach (var seed in seedPlans)
        {
            var existing = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == seed.Code, cancellationToken);
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

    private async Task LogEventAsync(User user, Guid? subscriptionId, string eventType, string? notes, string? data, CancellationToken cancellationToken)
    {
        _db.SubscriptionEvents.Add(new SubscriptionEventLog
        {
            Id = Guid.NewGuid(), // Generate UUID client-side to avoid RETURNING clause
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
