namespace KSignal.API.Messaging;

public record SynchronizeMarketData(string? SeriesId, string? Cursor, string? MarketTickerId = null);
