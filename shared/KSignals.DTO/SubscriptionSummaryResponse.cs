namespace KSignals.DTO;

public class SubscriptionSummaryResponse
{
    public SubscriptionPlanDto? ActivePlan { get; set; }
    public string Status { get; set; } = "none";
    public DateTime? CurrentPeriodEnd { get; set; }
    public string? StripeCustomerId { get; set; }
    public List<SubscriptionEventDto> Events { get; set; } = new();
}
