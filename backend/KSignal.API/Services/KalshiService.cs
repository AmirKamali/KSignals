using System.Linq;
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

        var connectionString = configuration.GetConnectionString("KalshiClickHouse");
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
                    Markets = new List<MarketSnapshot>(),
                    TotalCount = 0,
                    TotalPages = 0,
                    CurrentPage = 1,
                    PageSize = Math.Max(1, pageSize)
                };
            }
        }

        var nowUtc = DateTime.UtcNow;
        var maxCloseTime = GetMaxCloseTimeFromDateType(closeDateType, nowUtc);

        // Get the most recent snapshot for each ticker using a subquery
        var marketsQuery = _db.MarketSnapshots
            .AsNoTracking()
            .Where(p => p.CloseTime > nowUtc && p.Status == status)
            .Where(p => p.GenerateDate == _db.MarketSnapshots
                .Where(s => s.Ticker == p.Ticker && s.CloseTime > nowUtc && s.Status == status)
                .Select(s => s.GenerateDate)
                .DefaultIfEmpty()
                .Max());

         if (seriesIds != null)
        {
            marketsQuery = marketsQuery.Where(p => seriesIds.Contains(p.EventTicker));
        }

        if (maxCloseTime.HasValue)
        {
            marketsQuery = marketsQuery.Where(p => p.CloseTime <= maxCloseTime.Value);
        }

        if (searchTerm != null)
        {
            var likePattern = $"%{searchTerm}%";
            marketsQuery = marketsQuery.Where(p =>
                (p.YesSubTitle != null && EF.Functions.Like(p.YesSubTitle, likePattern)) ||
                (p.NoSubTitle != null && EF.Functions.Like(p.NoSubTitle, likePattern)) ||
                (p.EventTicker != null && EF.Functions.Like(p.EventTicker, likePattern)) ||
                (p.Ticker != null && EF.Functions.Like(p.Ticker, likePattern)));
        }

        if (sortBy == MarketSort.Volume)
        {
            marketsQuery = direction == SortDirection.Asc
                ? marketsQuery.OrderBy(m => m.Volume24h)
                : marketsQuery.OrderByDescending(m => m.Volume24h);
        }



        var safePageSize = Math.Max(1, pageSize);
        var totalCount = await marketsQuery.Select(m => m.Ticker).CountAsync();
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

    public async Task<MarketSnapshot?> GetMarketDetailsAsync(string tickerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tickerId))
        {
            throw new ArgumentException("TickerId is required", nameof(tickerId));
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);

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
            
            // Look up the seriesId from market_events table using EventTicker
            var seriesId = string.Empty;
            if (!string.IsNullOrWhiteSpace(market.EventTicker))
            {
                var marketEvent = await _db.MarketEvents
                    .AsNoTracking()
                    .Where(e => e.EventTicker == market.EventTicker)
                    .FirstOrDefaultAsync(cancellationToken);
                seriesId = marketEvent?.SeriesTicker ?? string.Empty;
            }
            
            var mapped = MapMarket(seriesTicker ?? tickerId, seriesId, market, fetchedAtUtc);

            // Find the most recent snapshot for this ticker
            var existing = await _db.MarketSnapshots
                .Where(m => m.Ticker == mapped.Ticker)
                .OrderByDescending(m => m.GenerateDate)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (existing == null)
            {
                // Generate ID manually (ClickHouse doesn't support auto-increment)
                var maxId = await _db.MarketSnapshots.MaxAsync(s => (ulong?)s.MarketSnapshotID, cancellationToken) ?? 0;
                mapped.MarketSnapshotID = maxId + 1;
                await _db.MarketSnapshots.AddAsync(mapped, cancellationToken);
            }
            else
            {
                CopyMarketSnapshot(existing, mapped);
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

    public async Task<(MarketSnapshot? Market, MarketCategory? Category)> GetMarketDetailsWithCategoryAsync(string tickerId, CancellationToken cancellationToken = default)
    {
        var market = await GetMarketDetailsAsync(tickerId, cancellationToken);
        if (market == null)
        {
            return (null, null);
        }

        // Derive series id from tickerId by taking the first component before '-'
        var seriesId = string.Empty;
        if (!string.IsNullOrWhiteSpace(market.Ticker))
        {
            var parts = market.Ticker.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            seriesId = parts.Length > 0 ? parts[0] : market.Ticker;
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

    private static void CopyMarketSnapshot(MarketSnapshot target, MarketSnapshot source)
    {
        target.Ticker = source.Ticker;
        target.EventTicker = source.EventTicker;
        target.MarketType = source.MarketType;
        target.YesSubTitle = source.YesSubTitle;
        target.NoSubTitle = source.NoSubTitle;
        target.CreatedTime = source.CreatedTime;
        target.OpenTime = source.OpenTime;
        target.CloseTime = source.CloseTime;
        target.ExpectedExpirationTime = source.ExpectedExpirationTime;
        target.LatestExpirationTime = source.LatestExpirationTime;
        target.SettlementTimerSeconds = source.SettlementTimerSeconds;
        target.Status = source.Status;
        target.ResponsePriceUnits = source.ResponsePriceUnits;
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
        target.Volume = source.Volume;
        target.Volume24h = source.Volume24h;
        target.Result = source.Result;
        target.CanCloseEarly = source.CanCloseEarly;
        target.OpenInterest = source.OpenInterest;
        target.NotionalValue = source.NotionalValue;
        target.NotionalValueDollars = source.NotionalValueDollars;
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
        target.ExpirationValue = source.ExpirationValue;
        target.FeeWaiverExpirationTime = source.FeeWaiverExpirationTime;
        target.EarlyCloseCondition = source.EarlyCloseCondition;
        target.TickSize = source.TickSize;
        target.StrikeType = source.StrikeType;
        target.FloorStrike = source.FloorStrike;
        target.CapStrike = source.CapStrike;
        target.FunctionalStrike = source.FunctionalStrike;
        target.CustomStrike = source.CustomStrike;
        target.RulesPrimary = source.RulesPrimary;
        target.RulesSecondary = source.RulesSecondary;
        target.MveCollectionTicker = source.MveCollectionTicker;
        target.MveSelectedLegs = source.MveSelectedLegs;
        target.PrimaryParticipantKey = source.PrimaryParticipantKey;
        target.PriceLevelStructure = source.PriceLevelStructure;
        target.PriceRanges = source.PriceRanges;
        target.GenerateDate = source.GenerateDate;
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

    internal static MarketSnapshot MapMarket(string seriesTicker, string seriesId, Market market, DateTime generateDate)
    {
        return new MarketSnapshot
        {
            Ticker = market.Ticker,
            SeriesId = seriesId,
            EventTicker = market.EventTicker,
            MarketType = market.MarketType.ToString(),
            YesSubTitle = market.YesSubTitle,
            NoSubTitle = market.NoSubTitle,
            CreatedTime = market.CreatedTime,
            OpenTime = market.OpenTime,
            CloseTime = market.CloseTime,
            ExpectedExpirationTime = market.ExpectedExpirationTime,
            LatestExpirationTime = market.LatestExpirationTime,
            SettlementTimerSeconds = market.SettlementTimerSeconds,
            Status = market.Status.ToString(),
            ResponsePriceUnits = market.ResponsePriceUnits.ToString(),
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
            Volume = market.Volume,
            Volume24h = market.Volume24h,
            Result = market.Result.ToString(),
            CanCloseEarly = market.CanCloseEarly,
            OpenInterest = market.OpenInterest,
            NotionalValue = market.NotionalValue,
            NotionalValueDollars = market.NotionalValueDollars,
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
            ExpirationValue = market.ExpirationValue,
            FeeWaiverExpirationTime = market.FeeWaiverExpirationTime,
            EarlyCloseCondition = market.EarlyCloseCondition,
            TickSize = market.TickSize,
            StrikeType = market.StrikeType?.ToString(),
            FloorStrike = market.FloorStrike,
            CapStrike = market.CapStrike,
            FunctionalStrike = market.FunctionalStrike,
            CustomStrike = market.CustomStrike != null ? JsonConvert.SerializeObject(market.CustomStrike) : null,
            RulesPrimary = market.RulesPrimary,
            RulesSecondary = market.RulesSecondary,
            MveCollectionTicker = market.MveCollectionTicker,
            MveSelectedLegs = market.MveSelectedLegs != null && market.MveSelectedLegs.Any() ? JsonConvert.SerializeObject(market.MveSelectedLegs) : null,
            PrimaryParticipantKey = market.PrimaryParticipantKey,
            PriceLevelStructure = market.PriceLevelStructure,
            PriceRanges = market.PriceRanges != null && market.PriceRanges.Any() ? JsonConvert.SerializeObject(market.PriceRanges) : null,
            GenerateDate = generateDate
        };
    }


}
