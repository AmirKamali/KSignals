namespace KSignal.API.Messaging;

/// <summary>
/// Message to trigger cleanup of market data for a specific ticker
/// </summary>
/// <param name="TickerId">The market ticker to clean up</param>
public record CleanupMarketData(string TickerId);
