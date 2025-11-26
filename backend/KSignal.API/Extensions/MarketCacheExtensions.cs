using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KSignal.API.Extensions;

public static class MarketCacheExtensions
{
    /// <summary>
    /// Projects MarketCache entities to exclude the JsonResponse column, selecting only specific columns from the database.
    /// </summary>
    public static IQueryable<MarketCache> SelectWithoutJsonResponse(this IQueryable<MarketCache> query)
    {
        return query.Select(m => new MarketCache
        {
            TickerId = m.TickerId,
            SeriesTicker = m.SeriesTicker,
            Title = m.Title,
            Subtitle = m.Subtitle,
            Volume = m.Volume,
            Volume24h = m.Volume24h,
            CreatedTime = m.CreatedTime,
            ExpirationTime = m.ExpirationTime,
            CloseTime = m.CloseTime,
            LatestExpirationTime = m.LatestExpirationTime,
            OpenTime = m.OpenTime,
            Status = m.Status,
            YesBid = m.YesBid,
            YesBidDollars = m.YesBidDollars,
            YesAsk = m.YesAsk,
            YesAskDollars = m.YesAskDollars,
            NoBid = m.NoBid,
            NoBidDollars = m.NoBidDollars,
            NoAsk = m.NoAsk,
            NoAskDollars = m.NoAskDollars,
            LastPrice = m.LastPrice,
            LastPriceDollars = m.LastPriceDollars,
            PreviousYesBid = m.PreviousYesBid,
            PreviousYesBidDollars = m.PreviousYesBidDollars,
            PreviousYesAsk = m.PreviousYesAsk,
            PreviousYesAskDollars = m.PreviousYesAskDollars,
            PreviousPrice = m.PreviousPrice,
            PreviousPriceDollars = m.PreviousPriceDollars,
            Liquidity = m.Liquidity,
            LiquidityDollars = m.LiquidityDollars,
            SettlementValue = m.SettlementValue,
            SettlementValueDollars = m.SettlementValueDollars,
            NotionalValue = m.NotionalValue,
            NotionalValueDollars = m.NotionalValueDollars,
            JsonResponse = null,
            LastUpdate = m.LastUpdate
        });
    }
}

