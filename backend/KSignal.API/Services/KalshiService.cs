using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Extensions;
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

    public async Task<MarketPageResult> GetMarketsAsync(
        string? category = null,
        string? tag = null,
        string? closeDateType = "next_24_hr",
        MarketSort sortBy = MarketSort.Volume,
        SortDirection direction = SortDirection.Desc,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var hasCategoryOrTag = !string.IsNullOrWhiteSpace(category) || !string.IsNullOrWhiteSpace(tag);
        HashSet<string>? seriesIds = null;

        if (hasCategoryOrTag)
        {
            // Read `marketcategories` to find series matching the filters and aggregate seriesIds
            seriesIds = new HashSet<string>(_db.MarketCategories
                .AsNoTracking()
                .Where(mc =>
                    (string.IsNullOrWhiteSpace(category) || mc.Category == category) &&
                    (string.IsNullOrWhiteSpace(tag) || (mc.Tags != null && mc.Tags.Contains(tag))))
                .Select(mc => mc.SeriesId)
                .Distinct()
                .ToList());

            if (seriesIds.Count == 0)
            {
                _logger.LogWarning("No series found for category={Category}, tag={Tag}", category, tag);
                return new MarketPageResult
                {
                    Markets = new List<MarketCache>(),
                    TotalCount = 0,
                    TotalPages = 0,
                    CurrentPage = 1,
                    PageSize = Math.Max(1, pageSize)
                };
            }
        }

        var nowUtc = DateTime.UtcNow;
        var maxCloseTime = GetMaxCloseTimeFromDateType(closeDateType, nowUtc);

        var query = _db.Markets
            .AsNoTracking()
            .Where(p => p.CloseTime > nowUtc);

        // if (seriesIds != null)
        {
            query = query.Where(p => seriesIds.Contains(p.SeriesTicker));
        }

        if (maxCloseTime.HasValue)
        {
            query = query.Where(p => p.CloseTime <= maxCloseTime.Value);
        }

        // query = query
        //     .GroupBy(m => m.SeriesTicker)
        //     .Select(g => g.OrderByDescending(x => x.Volume24h).First());

        var safePageSize = Math.Max(1, pageSize);
        var totalCount = await query.Select(m => m.TickerId).CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)safePageSize);
        var safePage = totalPages > 0 ? Math.Min(Math.Max(1, page), totalPages) : 1;
        var skip = (safePage - 1) * safePageSize;

        if (sortBy == MarketSort.Volume)
        {
            query = direction == SortDirection.Asc
                ? query.OrderBy(m => m.Volume24h)
                : query.OrderByDescending(m => m.Volume24h);
        }

        var markets = await query
            .Skip(skip)
            .Take(safePageSize)
            .Select(m => new MarketCache
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
            })
            .ToListAsync(cancellationToken);
        return new MarketPageResult
            {
                Markets = markets,
                TotalCount = totalCount,
                TotalPages = totalPages,
                CurrentPage = safePage,
                PageSize = safePageSize
            };
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


}
