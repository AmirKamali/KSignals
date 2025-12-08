using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

public class SubscriptionEventLog
{
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public ulong UserId { get; set; }

    [MaxLength(64)]
    public string? SubscriptionId { get; set; }

    [Required]
    [MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Notes { get; set; }

    public string? Data { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
