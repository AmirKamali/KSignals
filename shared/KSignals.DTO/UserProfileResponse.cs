namespace KSignals.DTO;

public class UserProfileResponse
{
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string SubscriptionStatus { get; set; } = "none";
    public string? ActivePlanId { get; set; }
    public string? ActivePlanCode { get; set; }
    public string? ActivePlanName { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
}
