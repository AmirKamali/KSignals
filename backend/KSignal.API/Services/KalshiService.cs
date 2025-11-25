using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KSignal.API.Services;

public class KalshiService
{
    private readonly KalshiClient _kalshiClient;
    private readonly ILogger<KalshiService> _logger;
    private readonly KalshiDbContext _db;

    public KalshiService(KalshiClient kalshiClient, ILogger<KalshiService> logger, KalshiDbContext db, IConfiguration configuration)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _db = db ?? throw new ArgumentNullException(nameof(db));

        var connectionString = configuration.GetConnectionString("KalshiMySql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is missing. Set KALSHI_DB_HOST, KALSHI_DB_USER, KALSHI_DB_PASSWORD, and KALSHI_DB_NAME or KALSHI_DB_CONNECTION.");
        }
    }

    public async Task<List<MarketCache>> GetMarketsAsync(string? category = null, string? tag = null, string? closeDateType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(tag))
        {
            return await GetTodayMarketsAsync(cancellationToken);
        }

        // Read `marketcategories` to find series matching the filters and aggregate seriesIds
        var seriesIds = new HashSet<string>(_db.MarketCategories
            .AsNoTracking()
            .Where(mc =>
                (string.IsNullOrWhiteSpace(category) || mc.Category == category) &&
                (string.IsNullOrWhiteSpace(tag) || (mc.Tags != null && mc.Tags.Contains(tag))))
            .Select(mc => mc.SeriesId)
            .Distinct()
            .ToList());

        if (seriesIds == null || seriesIds.Count == 0)
        {
            _logger.LogWarning("No series found for category={Category}, tag={Tag}", category, tag);
            return new List<MarketCache>();
        }

        var nowUtc = DateTime.UtcNow;
        var maxCloseTime = GetMaxCloseTimeFromDateType(closeDateType, nowUtc);

        var query = _db.Markets
            .AsNoTracking()
            .Where(p => seriesIds.Contains(p.SeriesTicker) && p.CloseTime > nowUtc);

        if (maxCloseTime.HasValue)
        {
            query = query.Where(p => p.CloseTime <= maxCloseTime.Value);
        }

        var allMarkets = query.ToList();
        var fetchedAtUtc = nowUtc;

        

        // Group by series and take the market with highest 24h volume per series
        return allMarkets
            .GroupBy(m => m.SeriesTicker, StringComparer.OrdinalIgnoreCase)
            .Select(g => WithoutJsonResponse(g.OrderByDescending(x => x.Volume24h).First()))
            .OrderByDescending(m => m.Volume24h)
            .ToList();
    }

    private static DateTime? GetMaxCloseTimeFromDateType(string? closeDateType, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(closeDateType) || closeDateType.Equals("all_time", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var targetDate = nowUtc;

        switch (closeDateType.ToLowerInvariant())
        {
            case "next_24_hr":
                targetDate = nowUtc.AddHours(24);
                break;

            case "next_48_hr":
                targetDate = nowUtc.AddHours(48);
                break;

            case "next_7_days":
                targetDate = nowUtc.AddDays(7);
                break;

            case "next_30_days":
                targetDate = nowUtc.AddDays(30);
                break;

            case "next_90_days":
                targetDate = nowUtc.AddDays(90);
                break;

            case "this_year":
                targetDate = new DateTime(nowUtc.Year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                break;

            case "next_year":
                targetDate = new DateTime(nowUtc.Year + 1, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                break;

            default:
                return null;
        }

        return targetDate;
    }


    public async Task<List<MarketCache>> GetTodayMarketsAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var maxCloseTs = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds();
        var fetchedAtUtc = nowUtc;
        var results = new List<MarketCache>();
        string? cursor = null;

        do
        {
            var requestOptions = new RequestOptions
            {
                Operation = "MarketApi.GetMarkets"
            };

            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "limit", 1000));
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "status", "open"));
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "max_close_ts", maxCloseTs));
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "with_nested_markets", true));
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "cursor", cursor));
            }

            var response = await _kalshiClient.Markets.AsynchronousClient.GetAsync<GetMarketsResponse>(
                    "/markets",
                    requestOptions,
                    _kalshiClient.Markets.Configuration,
                    cancellationToken)
                .ConfigureAwait(false);

            var data = response?.Data;
            if (data?.Markets != null)
            {
                var mapped = data.Markets
                    .Where(m => m != null && m.CloseTime > nowUtc)
                    .Select(m =>
                    {
                        var seriesKey = string.IsNullOrWhiteSpace(m!.EventTicker) ? m.Ticker : m.EventTicker;
                        return MapMarket(seriesKey, m, fetchedAtUtc);
                    })
                    .ToList();
                results.AddRange(mapped);
            }

            cursor = data?.Cursor;
        } while (!string.IsNullOrWhiteSpace(cursor));

        var ordered = results.OrderBy(m => m.CloseTime).ToList();

        return ordered
            .GroupBy(m => m.SeriesTicker, StringComparer.OrdinalIgnoreCase)
            .Select(g => WithoutJsonResponse(g.OrderByDescending(x => x.Volume24h).First()))
            .OrderBy(m => m.CloseTime)
            .ToList();
    }

    private async Task EnsureMarketsTableAsync(CancellationToken cancellationToken)
    {
        const string createSql = @"
CREATE TABLE IF NOT EXISTS Markets (
    TickerId VARCHAR(255) NOT NULL,
    SeriesTicker VARCHAR(255) NOT NULL,
    Title TEXT,
    Subtitle TEXT,
    Volume INT,
    Volume24h INT,
    CreatedTime DATETIME,
    ExpirationTime DATETIME,
    CloseTime DATETIME,
    LatestExpirationTime DATETIME,
    OpenTime DATETIME,
    Status VARCHAR(64),
    YesBid DECIMAL(18,4),
    YesBidDollars VARCHAR(255),
    YesAsk DECIMAL(18,4),
    YesAskDollars VARCHAR(255),
    NoBid DECIMAL(18,4),
    NoBidDollars VARCHAR(255),
    NoAsk DECIMAL(18,4),
    NoAskDollars VARCHAR(255),
    LastPrice DECIMAL(18,4),
    LastPriceDollars VARCHAR(255),
    PreviousYesBid INT,
    PreviousYesBidDollars VARCHAR(255),
    PreviousYesAsk INT,
    PreviousYesAskDollars VARCHAR(255),
    PreviousPrice INT,
    PreviousPriceDollars VARCHAR(255),
    Liquidity INT,
    LiquidityDollars VARCHAR(255),
    SettlementValue INT,
    SettlementValueDollars VARCHAR(255),
    NotionalValue INT,
    NotionalValueDollars VARCHAR(255),
    JsonResponse LONGTEXT,
    LastUpdate DATETIME,
    PRIMARY KEY (TickerId),
    KEY idx_series_ticker (SeriesTicker)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await _db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    internal static MarketCache MapMarket(string seriesTicker, Market market, DateTime lastUpdate)
    {
        return new MarketCache
        {
            TickerId = market.Ticker,
            SeriesTicker = seriesTicker,
            Title = market.Title,
            Subtitle = market.Subtitle,
            Volume = market.Volume,
            Volume24h = market.Volume24h,
            CreatedTime = market.CreatedTime,
            ExpirationTime = market.ExpirationTime,
            CloseTime = market.CloseTime,
            LatestExpirationTime = market.LatestExpirationTime,
            OpenTime = market.OpenTime,
            Status = market.Status.ToString(),
            YesBid = market.YesBid,
            YesBidDollars = market.YesBidDollars,
            YesAsk = market.YesAsk,
            YesAskDollars = market.YesAskDollars,
            NoBid = market.NoBid,
            NoBidDollars = market.NoBidDollars,
            NoAsk = market.NoAsk,
            NoAskDollars = market.NoAskDollars,
            LastPrice = market.LastPrice,
            LastPriceDollars = market.LastPriceDollars,
            PreviousYesBid = market.PreviousYesBid,
            PreviousYesBidDollars = market.PreviousYesBidDollars,
            PreviousYesAsk = market.PreviousYesAsk,
            PreviousYesAskDollars = market.PreviousYesAskDollars,
            PreviousPrice = market.PreviousPrice,
            PreviousPriceDollars = market.PreviousPriceDollars,
            Liquidity = market.Liquidity,
            LiquidityDollars = market.LiquidityDollars,
            SettlementValue = market.SettlementValue,
            SettlementValueDollars = market.SettlementValueDollars,
            NotionalValue = market.NotionalValue,
            NotionalValueDollars = market.NotionalValueDollars,
            JsonResponse = JsonConvert.SerializeObject(market),
            LastUpdate = lastUpdate
        };
    }

    private static MarketCache WithoutJsonResponse(MarketCache market)
    {
        return new MarketCache
        {
            TickerId = market.TickerId,
            SeriesTicker = market.SeriesTicker,
            Title = market.Title,
            Subtitle = market.Subtitle,
            Volume = market.Volume,
            Volume24h = market.Volume24h,
            CreatedTime = market.CreatedTime,
            ExpirationTime = market.ExpirationTime,
            CloseTime = market.CloseTime,
            LatestExpirationTime = market.LatestExpirationTime,
            OpenTime = market.OpenTime,
            Status = market.Status,
            YesBid = market.YesBid,
            YesBidDollars = market.YesBidDollars,
            YesAsk = market.YesAsk,
            YesAskDollars = market.YesAskDollars,
            NoBid = market.NoBid,
            NoBidDollars = market.NoBidDollars,
            NoAsk = market.NoAsk,
            NoAskDollars = market.NoAskDollars,
            LastPrice = market.LastPrice,
            LastPriceDollars = market.LastPriceDollars,
            PreviousYesBid = market.PreviousYesBid,
            PreviousYesBidDollars = market.PreviousYesBidDollars,
            PreviousYesAsk = market.PreviousYesAsk,
            PreviousYesAskDollars = market.PreviousYesAskDollars,
            PreviousPrice = market.PreviousPrice,
            PreviousPriceDollars = market.PreviousPriceDollars,
            Liquidity = market.Liquidity,
            LiquidityDollars = market.LiquidityDollars,
            SettlementValue = market.SettlementValue,
            SettlementValueDollars = market.SettlementValueDollars,
            NotionalValue = market.NotionalValue,
            NotionalValueDollars = market.NotionalValueDollars,
            JsonResponse = null,
            LastUpdate = market.LastUpdate
        };
    }

}
