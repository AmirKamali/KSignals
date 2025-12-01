namespace KSignal.API.Messaging;

public record SynchronizeMarketData(string? Category, string? Cursor, string? MarketTickerId = null);
