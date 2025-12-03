using System;
using System.Collections.Generic;

namespace KSignals.DTO;

/// <summary>
/// Event data for client consumption
/// </summary>
public class ClientEventData
{
    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public string EventTicker { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for the series this event belongs to.
    /// </summary>
    public string SeriesTicker { get; set; } = string.Empty;

    /// <summary>
    /// Shortened descriptive title for the event.
    /// </summary>
    public string SubTitle { get; set; } = string.Empty;

    /// <summary>
    /// Full title of the event.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Event category (deprecated, use series-level category instead).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The specific date this event is based on. Only filled when the event uses a date strike.
    /// </summary>
    public DateTime? StrikeDate { get; set; }

    /// <summary>
    /// The time period this event covers (e.g., 'week', 'month').
    /// </summary>
    public string? StrikePeriod { get; set; }

    /// <summary>
    /// Array of markets associated with this event.
    /// </summary>
    public List<ClientEvent> Markets { get; set; } = new List<ClientEvent>();
}
