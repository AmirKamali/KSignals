namespace KSignals.DTO;

public class SubscriptionTierDto
{
    public string Tier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SubscriptionPlanDto? MonthlyPlan { get; set; }
    public SubscriptionPlanDto? AnnualPlan { get; set; }
}
