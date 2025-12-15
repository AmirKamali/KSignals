using System.ComponentModel.DataAnnotations;

namespace kadmin.Models;

public class MarketPriorityForm
{
    [Required]
    [MaxLength(255)]
    public string TickerId { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Priority { get; set; } = 1;

    public bool FetchCandlesticks { get; set; } = true;
    public bool FetchOrderbook { get; set; } = true;
    public bool ProcessAnalyticsL1 { get; set; }
    public bool ProcessAnalyticsL2 { get; set; }
    public bool ProcessAnalyticsL3 { get; set; }
}
