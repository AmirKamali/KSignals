namespace KSignal.API.Messaging;

public record SynchronizeMarketData(
    long? MinCreatedTs = null,
    long? MaxCreatedTs = null,
    string? Status = null,
    string? Cursor = null,
    string? MarketTickerId = null);
