namespace KSignal.API.Messaging;

/// <summary>
/// Message to trigger cleanup of market data for specific tickers
/// </summary>
/// <param name="TickerIds">The market tickers to clean up</param>
public record CleanupMarketData(string[] TickerIds);
