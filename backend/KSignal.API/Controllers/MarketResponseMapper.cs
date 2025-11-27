using System;
using System.Collections.Generic;
using System.Linq;
using KSignal.API.Models;

namespace KSignal.API.Controllers;

internal static class MarketResponseMapper
{
    public static IEnumerable<object> Shape(IEnumerable<MarketCache> markets, bool detailed = false)
    {
        if (markets == null) yield break;

        foreach (var m in markets)
        {
            yield return ToResponse(m, detailed);
        }
    }

    public static object ToResponse(MarketCache market, bool detailed = false, string? category = null, IEnumerable<string>? tags = null)
    {
        return new
        {
            market.TickerId,
            market.SeriesTicker,
            market.Title,
            market.Subtitle,
            market.Volume,
            market.Volume24h,
            market.CreatedTime,
            market.ExpirationTime,
            market.CloseTime,
            market.LatestExpirationTime,
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
            market.LastUpdate,
            Category = category,
            Tags = tags?.ToArray()
        };
    }
}
