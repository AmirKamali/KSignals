using System;
using System.Collections.Generic;
using System.Linq;
using KSignal.API.Models;
using KSignals.DTO;

namespace KSignal.API.Controllers;

internal static class MarketResponseMapper
{
    public static IEnumerable<object> Shape(IEnumerable<ClientEvent> markets, bool detailed = false)
    {
        if (markets == null) yield break;

        foreach (var m in markets)
        {
            yield return ToResponse(m, detailed);
        }
    }

    public static object ToResponse(ClientEvent market, bool detailed = false)
    {
        return new
        {
            // Event fields
            market.EventTicker,
            market.SeriesTicker,
            market.Title,
            market.SubTitle,
            market.Category,
            
            // Market identification
            market.Ticker,
            market.MarketType,
            market.YesSubTitle,
            market.NoSubTitle,
            
            // Time fields
            market.CreatedTime,
            market.OpenTime,
            market.CloseTime,
            market.ExpectedExpirationTime,
            market.LatestExpirationTime,
            market.Status,
            
            // Pricing fields
            market.YesBid,
            market.YesBidDollars,
            market.YesAsk,
            market.YesAskDollars,
            market.NoBid,
            market.NoBidDollars,
            market.NoAsk,
            market.NoAskDollars,
            market.LastPrice,
            market.LastPriceDollars,
            market.PreviousYesBid,
            market.PreviousYesBidDollars,
            market.PreviousYesAsk,
            market.PreviousYesAskDollars,
            market.PreviousPrice,
            market.PreviousPriceDollars,
            market.SettlementValue,
            market.SettlementValueDollars,
            
            // Volume fields
            market.Volume,
            market.Volume24h,
            market.OpenInterest,
            market.NotionalValue,
            market.NotionalValueDollars,
            
            // Liquidity fields
            market.Liquidity,
            market.LiquidityDollars,
            
            // Metadata
            market.GenerateDate
        };
    }

    /// <summary>
    /// Legacy method for MarketSnapshot responses (e.g., market details endpoint)
    /// </summary>
    public static object ToResponse(MarketSnapshot market, bool detailed = false, string? category = null, IEnumerable<string>? tags = null)
    {
        return new
        {
            market.MarketSnapshotID,
            market.Ticker,
            market.EventTicker,
            market.MarketType,
            market.YesSubTitle,
            market.NoSubTitle,
            market.Volume,
            market.Volume24h,
            market.CreatedTime,
            market.ExpectedExpirationTime,
            market.LatestExpirationTime,
            market.CloseTime,
            market.OpenTime,
            market.Status,
            market.YesBid,
            market.YesBidDollars,
            market.YesAsk,
            market.YesAskDollars,
            market.NoBid,
            market.NoBidDollars,
            market.NoAsk,
            market.NoAskDollars,
            market.LastPrice,
            market.LastPriceDollars,
            market.PreviousYesBid,
            market.PreviousYesBidDollars,
            market.PreviousYesAsk,
            market.PreviousYesAskDollars,
            market.PreviousPrice,
            market.PreviousPriceDollars,
            market.Liquidity,
            market.LiquidityDollars,
            market.SettlementValue,
            market.SettlementValueDollars,
            market.NotionalValue,
            market.NotionalValueDollars,
            market.GenerateDate,
            Category = category,
            Tags = tags?.ToArray()
        };
    }
}
