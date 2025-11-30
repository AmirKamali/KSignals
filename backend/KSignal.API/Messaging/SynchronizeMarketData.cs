namespace KSignal.API.Messaging;

public record SynchronizeMarketData(string? Cursor, string? MarketTickerId = null);
