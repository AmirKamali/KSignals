using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

public class MarketCategory
{
    [Key]
    public string SeriesId { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public string? Ticker { get; set; }
    public string? Title { get; set; }
    public string? Frequency { get; set; }
    public string? JsonResponse { get; set; }
    public DateTime LastUpdate { get; set; }
}

