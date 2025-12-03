using System;
using System.Collections.Generic;
using System.Linq;
using KSignal.API.Models;
using KSignals.DTO;
using Kalshi.Api.Model;

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

    /// <summary>
    /// Maps GetEventResponse to ClientEventDetailsResponse
    /// </summary>
    public static ClientEventDetailsResponse MapEventDetails(GetEventResponse eventResponse)
    {
        if (eventResponse?.Event == null)
        {
            throw new ArgumentNullException(nameof(eventResponse));
        }

        var eventData = eventResponse.Event;
        var clientEventData = new ClientEventData
        {
            EventTicker = eventData.EventTicker ?? string.Empty,
            SeriesTicker = eventData.SeriesTicker ?? string.Empty,
            SubTitle = eventData.SubTitle ?? string.Empty,
            Title = eventData.Title ?? string.Empty,
            Category = eventData.Category ?? string.Empty,
            StrikeDate = eventData.StrikeDate,
            StrikePeriod = eventData.StrikePeriod
        };

        var clientMarkets = new List<ClientEvent>();
        var markets = eventData.Markets ?? eventResponse.Markets ?? new List<Market>();

        foreach (var market in markets)
        {
            clientMarkets.Add(MapMarketToClientEvent(market, eventData));
        }

        return new ClientEventDetailsResponse
        {
            Event = clientEventData,
            Markets = clientMarkets
        };
    }

    private static ClientEvent MapMarketToClientEvent(Market market, EventData eventData)
    {
        return new ClientEvent
        {
            // Event fields from the parent event
            EventTicker = eventData.EventTicker ?? string.Empty,
            SeriesTicker = eventData.SeriesTicker ?? string.Empty,
            Title = eventData.Title ?? string.Empty,
            SubTitle = eventData.SubTitle ?? string.Empty,
            Category = eventData.Category ?? string.Empty,

            // Market identification
            Ticker = market.Ticker ?? string.Empty,
            MarketType = market.MarketType.ToString().ToLowerInvariant(),
            YesSubTitle = market.YesSubTitle ?? string.Empty,
            NoSubTitle = market.NoSubTitle ?? string.Empty,

            // Time fields
            CreatedTime = market.CreatedTime,
            OpenTime = market.OpenTime,
            CloseTime = market.CloseTime,
            ExpectedExpirationTime = market.ExpectedExpirationTime,
            LatestExpirationTime = market.LatestExpirationTime,
            Status = market.Status.ToString().ToLowerInvariant(),

            // Pricing fields - Market model uses decimal for prices (in cents)
            YesBid = market.YesBid,
            YesBidDollars = market.YesBidDollars ?? FormatDecimalCentsToDollars(market.YesBid),
            YesAsk = market.YesAsk,
            YesAskDollars = market.YesAskDollars ?? FormatDecimalCentsToDollars(market.YesAsk),
            NoBid = market.NoBid,
            NoBidDollars = market.NoBidDollars ?? FormatDecimalCentsToDollars(market.NoBid),
            NoAsk = market.NoAsk,
            NoAskDollars = market.NoAskDollars ?? FormatDecimalCentsToDollars(market.NoAsk),
            LastPrice = market.LastPrice,
            LastPriceDollars = market.LastPriceDollars ?? FormatDecimalCentsToDollars(market.LastPrice),
            PreviousYesBid = market.PreviousYesBid,
            PreviousYesBidDollars = market.PreviousYesBidDollars ?? FormatIntCentsToDollars(market.PreviousYesBid),
            PreviousYesAsk = market.PreviousYesAsk,
            PreviousYesAskDollars = market.PreviousYesAskDollars ?? FormatIntCentsToDollars(market.PreviousYesAsk),
            PreviousPrice = market.PreviousPrice,
            PreviousPriceDollars = market.PreviousPriceDollars ?? FormatIntCentsToDollars(market.PreviousPrice),
            SettlementValue = market.SettlementValue,
            SettlementValueDollars = market.SettlementValueDollars ?? (market.SettlementValue.HasValue ? FormatIntCentsToDollars(market.SettlementValue.Value) : null),

            // Volume fields
            Volume = market.Volume,
            Volume24h = market.Volume24h,
            OpenInterest = market.OpenInterest,
            NotionalValue = market.NotionalValue,
            NotionalValueDollars = market.NotionalValueDollars ?? FormatIntCentsToDollars(market.NotionalValue),

            // Liquidity fields
            Liquidity = market.Liquidity,
            LiquidityDollars = market.LiquidityDollars ?? FormatIntCentsToDollars(market.Liquidity),

            // Metadata
            GenerateDate = DateTime.UtcNow
        };
    }

    private static string FormatIntCentsToDollars(int cents)
    {
        return $"${(cents / 100.0):F2}";
    }

    private static string FormatDecimalCentsToDollars(decimal cents)
    {
        return $"${(cents / 100.0m):F2}";
    }
}
