namespace KSignals.DTO;

public class SignInResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = "none";
    public string? ActivePlanId { get; set; }
    public string? ActivePlanCode { get; set; }
    public string? ActivePlanName { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
}
