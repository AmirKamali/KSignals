using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

/// <summary>
/// Tracks high-priority markets for orderbook syncing
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
    /// Timestamp when this record was last updated
    /// </summary>
    public DateTime LastUpdate { get; set; }
}
