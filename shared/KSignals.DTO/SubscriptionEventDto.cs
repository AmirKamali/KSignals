namespace KSignals.DTO;

public class SubscriptionEventDto
{
    public string EventType { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
