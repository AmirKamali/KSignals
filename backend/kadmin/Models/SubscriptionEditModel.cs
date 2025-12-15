using System.ComponentModel.DataAnnotations;

namespace kadmin.Models;

public class SubscriptionEditModel
{
    public Guid? Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid PlanId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Status { get; set; } = "pending";

    public bool CancelAtPeriodEnd { get; set; }

    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }

    [MaxLength(255)]
    public string? StripeSubscriptionId { get; set; }

    [MaxLength(255)]
    public string? StripeCustomerId { get; set; }
}
