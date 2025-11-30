using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

/// <summary>
/// Tracks high-priority markets for orderbook and candlestick syncing
/// </summary>
public class MarketHighPriority
{
    /// <summary>
    /// Market ticker ID (primary key)
    /// </summary>
    [Key]
    [Required]
    [MaxLength(255)]
    public string TickerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Priority level (higher = more important, synced more frequently)
    /// </summary>
    public int Priority { get; set; }
    
    /// <summary>
    /// Whether to fetch candlestick data for this market
    /// </summary>
    public bool FetchCandlesticks { get; set; } = true;
    
    /// <summary>
    /// Whether to fetch orderbook data for this market
    /// </summary>
    public bool FetchOrderbook { get; set; } = true;
    
    /// <summary>
    /// Timestamp when this record was last updated
    /// </summary>
    public DateTime LastUpdate { get; set; }
}
