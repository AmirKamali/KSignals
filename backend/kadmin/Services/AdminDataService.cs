using KSignal.API.Data;
using KSignal.API.Models;
using KSignals.DTO;
using kadmin.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace kadmin.Services;

public class AdminDataService
{
    private readonly KalshiDbContext _db;
    private readonly ILogger<AdminDataService> _logger;
    private readonly string? _stripeApiKey;

    public AdminDataService(KalshiDbContext db, ILogger<AdminDataService> logger, IConfiguration configuration)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stripeApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")
                        ?? configuration.GetSection("Stripe")["SecretKey"];
    }

    public async Task<AdminDashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _db.Users.CountAsync(cancellationToken);
        var activeSubscriptions = await _db.UserSubscriptions.CountAsync(s => s.Status == "active", cancellationToken);
        var plansCount = await _db.SubscriptionPlans.CountAsync(cancellationToken);
        var highPriorityCount = await _db.MarketHighPriorities.CountAsync(cancellationToken);

        var highPriority = await _db.MarketHighPriorities.AsNoTracking()
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.TickerId)
            .Take(8)
            .ToListAsync(cancellationToken);

        var latestEvents = await _db.SubscriptionEvents.AsNoTracking()
            .Include(e => e.User)
            .OrderByDescending(e => e.CreatedAt)
            .Take(8)
            .ToListAsync(cancellationToken);

        var eventModels = latestEvents.Select(e => new SubscriptionEventViewModel
        {
            UserLabel = e.User != null
                ? $"{e.User.Email ?? e.User.Username ?? e.User.FirebaseId}"
                : e.UserId.ToString(),
            Event = new SubscriptionEventDto
            {
                EventType = e.EventType,
                Notes = e.Notes,
                CreatedAt = e.CreatedAt
            }
        }).ToList();

        return new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            ActiveSubscriptions = activeSubscriptions,
            Plans = plansCount,
            HighPriorityCount = highPriorityCount,
            HighPriority = highPriority,
            LatestEvents = eventModels
        };
    }

    public async Task<List<UserListItem>> GetUsersAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowered = search.ToLowerInvariant();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(lowered)) ||
                (u.Username != null && u.Username.ToLower().Contains(lowered)) ||
                u.FirebaseId.ToLower().Contains(lowered));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Take(250)
            .ToListAsync(cancellationToken);

        var planNames = await _db.SubscriptionPlans.AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return users.Select(u =>
        {
            var planName = u.ActivePlanId.HasValue && planNames.TryGetValue(u.ActivePlanId.Value, out var name)
                ? name
                : null;

            return new UserListItem
            {
                User = u,
                ActivePlanName = planName
            };
        }).ToList();
    }

    public async Task<UserDetailViewModel?> GetUserDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return null;
        }

        var plans = await _db.SubscriptionPlans.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        SubscriptionPlan? activePlan = null;
        if (user.ActivePlanId.HasValue)
        {
            activePlan = plans.FirstOrDefault(p => p.Id == user.ActivePlanId.Value);
        }

        UserSubscription? activeSubscription = null;
        if (user.ActiveSubscriptionId.HasValue)
        {
            activeSubscription = await _db.UserSubscriptions.AsNoTracking()
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.Id == user.ActiveSubscriptionId.Value, cancellationToken);
        }

        var profile = new UserProfileResponse
        {
            Username = user.Username ?? string.Empty,
            FirstName = user.FirstName ?? string.Empty,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            SubscriptionStatus = user.SubscriptionStatus,
            ActivePlanId = activePlan?.Id.ToString() ?? user.ActivePlanId?.ToString(),
            ActivePlanCode = activePlan?.Code ?? activeSubscription?.Plan?.Code,
            ActivePlanName = activePlan?.Name ?? activeSubscription?.Plan?.Name,
            CurrentPeriodEnd = activeSubscription?.CurrentPeriodEnd
        };

        var subscriptions = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => s.UserId == id)
            .Include(s => s.Plan)
            .OrderByDescending(s => s.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var events = await _db.SubscriptionEvents.AsNoTracking()
            .Where(e => e.UserId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Take(40)
            .ToListAsync(cancellationToken);

        return new UserDetailViewModel
        {
            Form = new UserEditModel
            {
                Id = user.Id,
                FirebaseId = user.FirebaseId,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                IsComnEmailOn = user.IsComnEmailOn,
                ActiveSubscriptionId = user.ActiveSubscriptionId,
                ActivePlanId = user.ActivePlanId,
                SubscriptionStatus = user.SubscriptionStatus,
                StripeCustomerId = user.StripeCustomerId
            },
            Profile = profile,
            Subscriptions = subscriptions,
            Events = events,
            Plans = plans
        };
    }

    public async Task<Guid?> SaveUserAsync(UserEditModel model, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        User? user;

        if (model.Id.HasValue)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Id == model.Id.Value, cancellationToken);
            if (user == null)
            {
                return null;
            }
        }
        else
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                FirebaseId = model.FirebaseId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.Users.Add(user);
        }

        user.FirebaseId = model.FirebaseId;
        user.Username = model.Username;
        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Email = model.Email;
        user.IsComnEmailOn = model.IsComnEmailOn;
        user.SubscriptionStatus = string.IsNullOrWhiteSpace(model.SubscriptionStatus) ? "none" : model.SubscriptionStatus.Trim();
        user.ActivePlanId = model.ActivePlanId;
        user.ActiveSubscriptionId = model.ActiveSubscriptionId;
        user.StripeCustomerId = model.StripeCustomerId;
        user.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public async Task<List<UserSubscription>> GetSubscriptionsAsync(Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.UserSubscriptions.AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Plan)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(s => s.UserId == userId.Value);
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserSubscription?> GetSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.UserSubscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Guid?> SaveSubscriptionAsync(SubscriptionEditModel model, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        UserSubscription? subscription;

        if (model.Id.HasValue)
        {
            subscription = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.Id == model.Id.Value, cancellationToken);
            if (subscription == null)
            {
                return null;
            }
        }
        else
        {
            subscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                UpdatedAt = now,
                UserId = model.UserId
            };
            _db.UserSubscriptions.Add(subscription);
        }

        subscription.PlanId = model.PlanId;
        subscription.Status = model.Status.Trim();
        subscription.CancelAtPeriodEnd = model.CancelAtPeriodEnd;
        subscription.CurrentPeriodStart = model.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = model.CurrentPeriodEnd;
        subscription.StripeSubscriptionId = model.StripeSubscriptionId;
        subscription.StripeCustomerId = model.StripeCustomerId;
        subscription.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return subscription.Id;
    }

    public async Task<List<SubscriptionEventLog>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.SubscriptionEvents.AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.Subscription)
            .OrderByDescending(e => e.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AddEventAsync(EventCreateModel model, CancellationToken cancellationToken = default)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == model.UserId, cancellationToken);
        if (!userExists)
        {
            return false;
        }

        var entity = new SubscriptionEventLog
        {
            Id = Guid.NewGuid(),
            UserId = model.UserId,
            SubscriptionId = model.SubscriptionId,
            EventType = model.EventType.Trim(),
            Notes = model.Notes,
            Data = model.Data,
            CreatedAt = DateTime.UtcNow
        };

        _db.SubscriptionEvents.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<MarketHighPriority>> GetMarketHighPrioritiesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.MarketHighPriorities.AsNoTracking()
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.TickerId)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertMarketHighPriorityAsync(MarketPriorityForm model, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var existing = await _db.MarketHighPriorities.FirstOrDefaultAsync(m => m.TickerId == model.TickerId, cancellationToken);

        if (existing == null)
        {
            existing = new MarketHighPriority
            {
                TickerId = model.TickerId,
                LastUpdate = now
            };
            _db.MarketHighPriorities.Add(existing);
        }

        existing.Priority = model.Priority;
        existing.FetchCandlesticks = model.FetchCandlesticks;
        existing.FetchOrderbook = model.FetchOrderbook;
        existing.ProcessAnalyticsL1 = model.ProcessAnalyticsL1;
        existing.ProcessAnalyticsL2 = model.ProcessAnalyticsL2;
        existing.ProcessAnalyticsL3 = model.ProcessAnalyticsL3;
        existing.LastUpdate = now;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteMarketHighPriorityAsync(string tickerId, CancellationToken cancellationToken = default)
    {
        var entry = await _db.MarketHighPriorities.FirstOrDefaultAsync(m => m.TickerId == tickerId, cancellationToken);
        if (entry == null)
        {
            return false;
        }

        _db.MarketHighPriorities.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<List<SubscriptionPlan>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        return _db.SubscriptionPlans.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<List<User>> GetUsersForLookupAsync(CancellationToken cancellationToken = default)
    {
        return _db.Users.AsNoTracking()
            .OrderBy(u => u.Email ?? u.Username ?? u.FirebaseId)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionPlan?> GetPlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.SubscriptionPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Guid?> SavePlanAsync(PlanEditModel model, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        SubscriptionPlan plan;

        if (model.Id.HasValue)
        {
            plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == model.Id.Value, cancellationToken)
                   ?? throw new InvalidOperationException("Plan not found");
        }
        else
        {
            plan = new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.SubscriptionPlans.Add(plan);
        }

        plan.Code = model.Code.Trim();
        plan.Name = model.Name.Trim();
        plan.StripePriceId = model.StripePriceId.Trim();
        plan.Currency = model.Currency.Trim();
        plan.Interval = model.Interval.Trim();
        plan.Amount = model.Amount;
        plan.IsActive = model.IsActive;
        plan.Description = model.Description;
        plan.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return plan.Id;
    }

    public async Task<(decimal? amount, string? currency, string? interval, string? error)> RefreshPlanFromStripeAsync(
        PlanEditModel model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_stripeApiKey))
        {
            return (null, null, null, "Stripe secret key not configured (STRIPE_SECRET_KEY or Stripe:SecretKey).");
        }

        if (string.IsNullOrWhiteSpace(model.StripePriceId))
        {
            return (null, null, null, "Stripe price id is required.");
        }

        StripeConfiguration.ApiKey = _stripeApiKey;
        var service = new PriceService();

        try
        {
            var price = await service.GetAsync(model.StripePriceId, null, cancellationToken: cancellationToken);
            var amountMinor = price.UnitAmountDecimal ?? (price.UnitAmount.HasValue ? Convert.ToDecimal(price.UnitAmount.Value) : (decimal?)null);
            if (!amountMinor.HasValue)
            {
                return (null, null, null, "Stripe price missing amount.");
            }

            var amount = amountMinor.Value / 100m;
            var currency = price.Currency?.ToLowerInvariant();
            var interval = price.Recurring?.Interval;
            return (amount, currency, interval, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to fetch Stripe price {PriceId}", model.StripePriceId);
            return (null, null, null, $"Stripe error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Stripe price {PriceId}", model.StripePriceId);
            return (null, null, null, "Unexpected error fetching Stripe price.");
        }
    }
}
