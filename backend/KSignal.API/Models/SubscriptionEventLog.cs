using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KSignal.API.Models;

public class SubscriptionEventLog
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public Guid? SubscriptionId { get; set; }

    [ForeignKey(nameof(SubscriptionId))]
    public UserSubscription? Subscription { get; set; }

    [Required]
    [MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Notes { get; set; }

    public string? Data { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
