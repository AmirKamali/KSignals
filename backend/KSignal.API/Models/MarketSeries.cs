using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

/// <summary>
/// Represents a Kalshi series - a template for recurring events
/// </summary>
public class MarketSeries
{
    /// <summary>
    /// Ticker that identifies this series (primary key)
    /// </summary>
    [Key]
    [Required]
    [MaxLength(255)]
    public string Ticker { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of the frequency of the series (e.g., weekly, daily, one-off)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Frequency { get; set; } = string.Empty;
    
    /// <summary>
    /// Title describing the series
    /// </summary>
    [Required]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Category which this series belongs to
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Tags specifies the subjects that this series relates to (stored as JSON array)
    /// </summary>
    public string? Tags { get; set; }
    
    /// <summary>
    /// Settlement sources for market determination (stored as JSON array)
    /// </summary>
    public string? SettlementSources { get; set; }
    
    /// <summary>
    /// Direct link to the original filing of the contract
    /// </summary>
    public string? ContractUrl { get; set; }
    
    /// <summary>
    /// URL to the current terms of the contract
    /// </summary>
    public string? ContractTermsUrl { get; set; }
    
    /// <summary>
    /// Internal product metadata (stored as JSON)
    /// </summary>
    public string? ProductMetadata { get; set; }
    
    /// <summary>
    /// Fee structure type: quadratic, quadratic_with_maker_fees, or flat
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string FeeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Floating point multiplier applied to fee calculations
    /// </summary>
    public double FeeMultiplier { get; set; }
    
    /// <summary>
    /// Additional trading prohibitions for this series (stored as JSON array)
    /// </summary>
    public string? AdditionalProhibitions { get; set; }
    
    /// <summary>
    /// Timestamp when this record was last updated
    /// </summary>
    public DateTime LastUpdate { get; set; }
    
    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; }
}
