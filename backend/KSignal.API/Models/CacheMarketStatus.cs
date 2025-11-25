namespace KSignal.API.Models;

public class CacheMarketStatus
{
    public bool IsProcessing { get; set; }
    public int TotalJobs { get; set; }
    public int RemainingJobs { get; set; }
    public int CompletedJobs => TotalJobs - RemainingJobs;
    public DateTime? StartedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? Category { get; set; }
    public string? Tag { get; set; }
}

