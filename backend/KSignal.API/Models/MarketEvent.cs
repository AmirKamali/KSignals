using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

/// <summary>
/// Represents a Kalshi event - a specific instance within a series
/// </summary>
public class MarketEvent
{
    /// <summary>
    /// Unique identifier for this event (primary key)
    /// </summary>
    [Key]
    [Required]
    [MaxLength(255)]
    public string EventTicker { get; set; } = string.Empty;
    
    /// <summary>
    /// Unique identifier for the series this event belongs to
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string SeriesTicker { get; set; } = string.Empty;
    
    /// <summary>
    /// Shortened descriptive title for the event
    /// </summary>
    [Required]
    public string SubTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// Full title of the event
    /// </summary>
    [Required]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Specifies how collateral is returned when markets settle (e.g., 'binary')
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string CollateralReturnType { get; set; } = string.Empty;
    
    /// <summary>
    /// If true, only one market in this event can resolve to 'yes'
    /// </summary>
    public bool MutuallyExclusive { get; set; }
    
    /// <summary>
    /// Event category (deprecated, use series-level category instead)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// The specific date this event is based on (mutually exclusive with StrikePeriod)
    /// </summary>
    public DateTime? StrikeDate { get; set; }
    
    /// <summary>
    /// The time period this event covers (e.g., 'week', 'month')
    /// </summary>
    [MaxLength(50)]
    public string? StrikePeriod { get; set; }
    
    /// <summary>
    /// Whether this event is available to trade on brokers
    /// </summary>
    public bool AvailableOnBrokers { get; set; }
    
    /// <summary>
    /// Additional metadata for the event (stored as JSON)
    /// </summary>
    public string? ProductMetadata { get; set; }
    
    /// <summary>
    /// Timestamp when this record was last updated
    /// </summary>
    public DateTime LastUpdate { get; set; }
    
    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }
}
