using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

/// <summary>
/// Represents a synchronization job log entry
/// Tracks when sync jobs are enqueued for monitoring and auditing
/// </summary>
public class SyncLog
{
    /// <summary>
    /// Unique identifier for this log entry
    /// </summary>
    [Key]
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the synchronization event (e.g., "SynchronizeMarketData", "SynchronizeEvents")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// Number of messages/jobs enqueued for this sync operation
    /// </summary>
    [Required]
    public int NumbersEnqueued { get; set; }

    /// <summary>
    /// Log type/severity level: Info, WARN, ERROR, DEBUG
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Type { get; set; } = "Info";

    /// <summary>
    /// Component/class name that generated this log entry
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this log was generated in Debug mode (true) or Release mode (false)
    /// </summary>
    [Required]
    public bool IsDebug { get; set; }

    /// <summary>
    /// Timestamp when the sync job was enqueued
    /// </summary>
    [Required]
    public DateTime LogDate { get; set; }
}
