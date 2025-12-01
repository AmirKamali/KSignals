namespace KSignal.API.Messaging;

/// <summary>
/// Message to trigger analytics processing for a specific market ticker
/// </summary>
/// <param name="TickerId">The market ticker to process analytics for</param>
/// <param name="ProcessL1">Whether to process L1 analytics (basic features)</param>
/// <param name="ProcessL2">Whether to process L2 analytics (volatility/returns)</param>
/// <param name="ProcessL3">Whether to process L3 analytics (advanced metrics)</param>
public record ProcessMarketAnalytics(
    string TickerId,
    bool ProcessL1 = true,
    bool ProcessL2 = true,
    bool ProcessL3 = true
);
