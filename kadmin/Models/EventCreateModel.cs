using System.ComponentModel.DataAnnotations;

namespace kadmin.Models;

public class EventCreateModel
{
    [Required]
    public Guid UserId { get; set; }

    public Guid? SubscriptionId { get; set; }

    [Required]
    [MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Notes { get; set; }

    public string? Data { get; set; }
}
