using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

/// <summary>
/// Represents a change event in the orderbook (computed from snapshot differences).
/// Events are incremental and can be replayed to reconstruct order book state.
/// </summary>
public class OrderbookEvent
{
    /// <summary>
    /// Unique identifier for this event
    /// </summary>
    [Key]
    public long Id { get; set; }
    
    /// <summary>
    /// Market ticker ID
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string MarketId { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when this event occurred
    /// </summary>
    public DateTime EventTime { get; set; }
    
    /// <summary>
    /// Side of the orderbook: YES or NO
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Side { get; set; } = string.Empty;
    
    /// <summary>
    /// Price level where the change occurred
    /// </summary>
    public double Price { get; set; }
    
    /// <summary>
    /// Total size at this price level AFTER the event (not delta).
    /// - For 'add': the new size at this price level (> 0)
    /// - For 'update': the new total size at this level (> 0)
    /// - For 'remove': always 0 (level no longer exists)
    /// 
    /// To replay: set book[side, price] = size (or remove key if size == 0)
    /// </summary>
    public double Size { get; set; }
    
    /// <summary>
    /// Type of event:
    /// - 'add': A new price level appears (was 0 or missing, now > 0)
    /// - 'update': Price level size changed (was > 0, still > 0 but different)
    /// - 'remove': Price level disappeared (was > 0, now 0 or missing)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string EventType { get; set; } = string.Empty;
}
