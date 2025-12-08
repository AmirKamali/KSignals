namespace KSignals.DTO;

public class SubscriptionStatusResponse
{
    public bool HasActiveSubscription { get; set; }
    public bool IsUpgraded { get; set; }
    public SubscriptionPlanDto? Plan { get; set; }
    public string? Status { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public string? StripeCustomerId { get; set; }
}
