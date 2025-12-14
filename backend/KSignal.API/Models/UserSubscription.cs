using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KSignal.API.Models;

public class UserSubscription
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    public Guid PlanId { get; set; }

    [ForeignKey(nameof(PlanId))]
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
