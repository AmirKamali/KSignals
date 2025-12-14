namespace KSignal.API.Models;

/// <summary>
/// Log type/severity levels for sync logs
/// </summary>
public enum LogType
{
    /// <summary>
    /// Informational message
    /// </summary>
    Info,

    /// <summary>
    /// Warning message
    /// </summary>
    WARN,

    /// <summary>
    /// Error message
    /// </summary>
    ERROR,

    /// <summary>
    /// Debug message
    /// </summary>
    DEBUG
}
