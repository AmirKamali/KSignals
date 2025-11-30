using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        string? query = null,
        string? closeDateType = "next_30_days",
        string? status = "Active",  
        MarketSort sortBy = MarketSort.Volume,
        SortDirection direction = SortDirection.Desc,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // Default to 30-day window when caller does not provide a filter
        closeDateType ??= "next_30_days";
        var searchTerm = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

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

        var marketsQuery = _db.Markets
            .AsNoTracking()
            .Where(p => p.CloseTime > nowUtc && p.Status == status);

         if (seriesIds != null)
        {
            marketsQuery = marketsQuery.Where(p => seriesIds.Contains(p.SeriesTicker));
        }

        if (maxCloseTime.HasValue)
        {
            marketsQuery = marketsQuery.Where(p => p.CloseTime <= maxCloseTime.Value);
        }

        if (searchTerm != null)
        {
            var likePattern = $"%{searchTerm}%";
            marketsQuery = marketsQuery.Where(p =>
                (p.Title != null && EF.Functions.Like(p.Title, likePattern)) ||
                (p.Subtitle != null && EF.Functions.Like(p.Subtitle, likePattern)) ||
                (p.SeriesTicker != null && EF.Functions.Like(p.SeriesTicker, likePattern)) ||
                (p.TickerId != null && EF.Functions.Like(p.TickerId, likePattern)));
        }

        if (sortBy == MarketSort.Volume)
        {
            marketsQuery = direction == SortDirection.Asc
                ? marketsQuery.OrderBy(m => m.Volume24h)
                : marketsQuery.OrderByDescending(m => m.Volume24h);
        }



        var safePageSize = Math.Max(1, pageSize);
        var totalCount = await marketsQuery.Select(m => m.TickerId).CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)safePageSize);
        var safePage = totalPages > 0 ? Math.Min(Math.Max(1, page), totalPages) : 1;
        var skip = (safePage - 1) * safePageSize;

        var markets = await marketsQuery
            .Skip(skip)
            .Take(safePageSize)
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

    public async Task<MarketCache?> GetMarketDetailsAsync(string tickerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tickerId))
        {
            throw new ArgumentException("TickerId is required", nameof(tickerId));
        }

        await EnsureMarketsTableAsync(cancellationToken);

        try
        {
            var response = await _kalshiClient.Markets.GetMarketAsync(tickerId, cancellationToken: cancellationToken);
            var market = response?.Market;

            if (market == null)
            {
                _logger.LogWarning("Market {TickerId} was not returned from Kalshi", tickerId);
                return null;
            }

            var fetchedAtUtc = DateTime.UtcNow;
            var seriesTicker = string.IsNullOrWhiteSpace(market.EventTicker) ? market.Ticker : market.EventTicker;
            var mapped = MapMarket(seriesTicker ?? tickerId, market, fetchedAtUtc);

            var existing = await _db.Markets.FirstOrDefaultAsync(m => m.TickerId == mapped.TickerId, cancellationToken);
            if (existing == null)
            {
                await _db.Markets.AddAsync(mapped, cancellationToken);
            }
            else
            {
                CopyMarket(existing, mapped);
            }

            await _db.SaveChangesAsync(cancellationToken);

            return existing ?? mapped;
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error fetching market {TickerId}", tickerId);
            throw;
        }
    }

    public async Task<(MarketCache? Market, MarketCategory? Category)> GetMarketDetailsWithCategoryAsync(string tickerId, CancellationToken cancellationToken = default)
    {
        var market = await GetMarketDetailsAsync(tickerId, cancellationToken);
        if (market == null)
        {
            return (null, null);
        }

        // Derive series id from tickerId by taking the first component before '-'
        var seriesId = string.Empty;
        if (!string.IsNullOrWhiteSpace(market.TickerId))
        {
            var parts = market.TickerId.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            seriesId = parts.Length > 0 ? parts[0] : market.TickerId;
        }

        MarketCategory? category = null;

        try
        {
            category = await _db.MarketCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(mc => mc.SeriesId == seriesId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load market category for series {SeriesId}", seriesId);
        }

        return (market, category);
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
    LastUpdate DATETIME,
    PRIMARY KEY (TickerId),
    KEY idx_series_ticker (SeriesTicker)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";

        await _db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    private static void CopyMarket(MarketCache target, MarketCache source)
    {
        target.SeriesTicker = source.SeriesTicker;
        target.Title = source.Title;
        target.Subtitle = source.Subtitle;
        target.Volume = source.Volume;
        target.Volume24h = source.Volume24h;
        target.CreatedTime = source.CreatedTime;
        target.ExpirationTime = source.ExpirationTime;
        target.CloseTime = source.CloseTime;
        target.LatestExpirationTime = source.LatestExpirationTime;
        target.OpenTime = source.OpenTime;
        target.Status = source.Status;
        target.YesBid = source.YesBid;
        target.YesBidDollars = source.YesBidDollars;
        target.YesAsk = source.YesAsk;
        target.YesAskDollars = source.YesAskDollars;
        target.NoBid = source.NoBid;
        target.NoBidDollars = source.NoBidDollars;
        target.NoAsk = source.NoAsk;
        target.NoAskDollars = source.NoAskDollars;
        target.LastPrice = source.LastPrice;
        target.LastPriceDollars = source.LastPriceDollars;
        target.PreviousYesBid = source.PreviousYesBid;
        target.PreviousYesBidDollars = source.PreviousYesBidDollars;
        target.PreviousYesAsk = source.PreviousYesAsk;
        target.PreviousYesAskDollars = source.PreviousYesAskDollars;
        target.PreviousPrice = source.PreviousPrice;
        target.PreviousPriceDollars = source.PreviousPriceDollars;
        target.Liquidity = source.Liquidity;
        target.LiquidityDollars = source.LiquidityDollars;
        target.SettlementValue = source.SettlementValue;
        target.SettlementValueDollars = source.SettlementValueDollars;
        target.NotionalValue = source.NotionalValue;
        target.NotionalValueDollars = source.NotionalValueDollars;
        target.LastUpdate = source.LastUpdate;
    }

    internal static MarketCache MapMarket(string seriesTicker, Market market, DateTime lastUpdate)
    {
        var expirationTime = market.ExpectedExpirationTime ?? market.LatestExpirationTime;

#pragma warning disable CS0612 // title/subtitle are deprecated in Kalshi API; still used as fallback until replacements are provided in responses
        var legacyTitle = market.Title;
        var legacySubtitle = market.Subtitle;
#pragma warning restore CS0612

        return new MarketCache
        {
            TickerId = market.Ticker,
            SeriesTicker = seriesTicker,
            Title = string.IsNullOrWhiteSpace(market.YesSubTitle) ? legacyTitle : market.YesSubTitle,
            Subtitle = string.IsNullOrWhiteSpace(market.NoSubTitle) ? legacySubtitle : market.NoSubTitle,
            Volume = market.Volume,
            Volume24h = market.Volume24h,
            CreatedTime = market.CreatedTime,
            ExpirationTime = expirationTime,
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
            LastUpdate = lastUpdate
        };
    }


}
