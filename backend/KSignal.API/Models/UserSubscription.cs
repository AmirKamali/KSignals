using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

public class UserSubscription
{
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public ulong UserId { get; set; }

    public User? User { get; set; }

    [Required]
    [MaxLength(64)]
    public string PlanId { get; set; } = string.Empty;

    public SubscriptionPlan? Plan { get; set; }

    [MaxLength(255)]
    public string? StripeSubscriptionId { get; set; }

    [MaxLength(255)]
    public string? StripeCustomerId { get; set; }

    [MaxLength(64)]
    public string Status { get; set; } = "pending";

    public bool CancelAtPeriodEnd { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
