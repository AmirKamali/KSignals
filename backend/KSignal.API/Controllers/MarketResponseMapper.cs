using System;
using System.Collections.Generic;
using System.Linq;
using KSignal.API.Models;

namespace KSignal.API.Controllers;

internal static class MarketResponseMapper
{
    public static IEnumerable<object> Shape(IEnumerable<MarketSnapshot> markets, bool detailed = false)
    {
        if (markets == null) yield break;

        foreach (var m in markets)
        {
            yield return ToResponse(m, detailed);
        }
    }

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
