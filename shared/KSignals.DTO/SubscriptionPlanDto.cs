namespace KSignals.DTO;

public class SubscriptionPlanDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public string Interval { get; set; } = "month";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
