using System.Linq;
using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Models;
using KSignals.DTO;
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

    public async Task<ClientEventPageResult> GetEventsAsync(
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
        closeDateType ??= "next_30_days";
        var searchTerm = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        var nowUtc = DateTime.UtcNow;
        var maxCloseTime = GetMaxCloseTimeFromDateType(closeDateType, nowUtc);

        // Start from MarketEvents, join with MarketSeries and MarketSnapshotsLatest
        var baseQuery = from evt in _db.MarketEvents.AsNoTracking()
                        join series in _db.MarketSeries.AsNoTracking()
                            on evt.SeriesTicker equals series.Ticker into seriesJoin
                        from ser in seriesJoin.DefaultIfEmpty()
                        join snap in _db.MarketSnapshotsLatest.AsNoTracking()
                            on evt.EventTicker equals snap.EventTicker into snapJoin
                        from s in snapJoin
                        where !evt.IsDeleted
                           && s.CloseTime > nowUtc
                           && s.Status == status
                        select new { Event = evt, Series = ser, Snapshot = s };

        // Apply category/tag filter via MarketSeries
        if (!string.IsNullOrWhiteSpace(category))
            baseQuery = baseQuery.Where(x => x.Series != null && x.Series.Category == category);

        if (!string.IsNullOrWhiteSpace(tag))
            baseQuery = baseQuery.Where(x => x.Series != null && x.Series.Tags != null && x.Series.Tags.Contains(tag));

        // Apply close time filter
        if (maxCloseTime.HasValue)
            baseQuery = baseQuery.Where(x => x.Snapshot == null || x.Snapshot.CloseTime <= maxCloseTime.Value);

        // Apply search filter
        if (searchTerm != null)
        {
            var likePattern = $"%{searchTerm}%";
            baseQuery = baseQuery.Where(x =>
                EF.Functions.Like(x.Event.Title, likePattern) ||
                EF.Functions.Like(x.Event.SubTitle, likePattern) ||
                EF.Functions.Like(x.Snapshot.YesSubTitle, likePattern) ||
                EF.Functions.Like(x.Snapshot.NoSubTitle, likePattern) ||
                EF.Functions.Like(x.Event.EventTicker, likePattern) ||
                EF.Functions.Like(x.Snapshot.Ticker, likePattern));
        }

        // Execute query and get all results
        var results = await baseQuery.ToListAsync(cancellationToken);

        // Group by ticker and keep only latest snapshot per ticker
        var clientEvents = results
            .Select(x => new ClientEvent
            {
                EventTicker = x.Event.EventTicker,
                SeriesTicker = x.Event.SeriesTicker,
                Title = x.Event.Title,
                SubTitle = x.Event.SubTitle,
                Category = x.Event.Category,

                Ticker = x.Snapshot.Ticker,
                MarketType = x.Snapshot.MarketType,
                YesSubTitle = x.Snapshot.YesSubTitle,
                NoSubTitle = x.Snapshot.NoSubTitle,

                CreatedTime = x.Snapshot.CreatedTime,
                OpenTime = x.Snapshot.OpenTime,
                CloseTime = x.Snapshot.CloseTime,
                ExpectedExpirationTime = x.Snapshot.ExpectedExpirationTime,
                LatestExpirationTime = x.Snapshot.LatestExpirationTime,
                Status = x.Snapshot.Status,

                YesBid = x.Snapshot.YesBid,
                YesBidDollars = x.Snapshot.YesBidDollars,
                YesAsk = x.Snapshot.YesAsk,
                YesAskDollars = x.Snapshot.YesAskDollars,
                NoBid = x.Snapshot.NoBid,
                NoBidDollars = x.Snapshot.NoBidDollars,
                NoAsk = x.Snapshot.NoAsk,
                NoAskDollars = x.Snapshot.NoAskDollars,
                LastPrice = x.Snapshot.LastPrice,
                LastPriceDollars = x.Snapshot.LastPriceDollars,
                PreviousYesBid = x.Snapshot.PreviousYesBid,
                PreviousYesBidDollars = x.Snapshot.PreviousYesBidDollars,
                PreviousYesAsk = x.Snapshot.PreviousYesAsk,
                PreviousYesAskDollars = x.Snapshot.PreviousYesAskDollars,
                PreviousPrice = x.Snapshot.PreviousPrice,
                PreviousPriceDollars = x.Snapshot.PreviousPriceDollars,
                SettlementValue = x.Snapshot.SettlementValue,
                SettlementValueDollars = x.Snapshot.SettlementValueDollars,

                Volume = x.Snapshot.Volume,
                Volume24h = x.Snapshot.Volume24h,
                OpenInterest = x.Snapshot.OpenInterest,
                NotionalValue = x.Snapshot.NotionalValue,
                NotionalValueDollars = x.Snapshot.NotionalValueDollars,

                Liquidity = x.Snapshot.Liquidity,
                LiquidityDollars = x.Snapshot.LiquidityDollars,

                GenerateDate = x.Snapshot.GenerateDate
            })
            .ToList();

        // Apply sorting
        IEnumerable<ClientEvent> sortedEvents = sortBy == MarketSort.Volume
            ? (direction == SortDirection.Asc
                ? clientEvents.OrderBy(e => e.Volume24h)
                : clientEvents.OrderByDescending(e => e.Volume24h))
            : clientEvents;

        // Pagination
        var safePageSize = Math.Max(1, pageSize);
        var totalCount = clientEvents.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)safePageSize);
        var safePage = totalPages > 0 ? Math.Min(Math.Max(1, page), totalPages) : 1;

        var pagedEvents = sortedEvents
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        return new ClientEventPageResult
        {
            Markets = pagedEvents,
            TotalCount = totalCount,
            TotalPages = totalPages,
            CurrentPage = safePage,
            PageSize = safePageSize
        };
    }

    public async Task<ClientEvent?> GetMarketByTickerAsync(string ticker, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            throw new ArgumentException("Ticker is required", nameof(ticker));
        }

        try
        {
            // Fetch fresh market data from Kalshi API
            _logger.LogInformation("Fetching market {Ticker} from Kalshi API", ticker);
            var response = await _kalshiClient.Markets.GetMarketAsync(ticker, cancellationToken: cancellationToken);

            if (response?.Market == null)
            {
                _logger.LogWarning("Market {Ticker} was not returned from Kalshi API", ticker);
                return null;
            }

            var market = response.Market;
            var fetchedAtUtc = DateTime.UtcNow;

            // Save the market snapshot to database
            var newSnapshot = MapMarket(market, fetchedAtUtc);
            await _db.MarketSnapshots.AddAsync(newSnapshot, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Saved market snapshot for {Ticker} to database", ticker);

            // Query the full data from database (event + series + snapshot)
            var result = await (from evt in _db.MarketEvents.AsNoTracking()
                                join series in _db.MarketSeries.AsNoTracking()
                                    on evt.SeriesTicker equals series.Ticker into seriesJoin
                                from ser in seriesJoin.DefaultIfEmpty()
                                join snap in _db.MarketSnapshotsLatest.AsNoTracking()
                                    on evt.EventTicker equals snap.EventTicker into snapJoin
                                from s in snapJoin
                                where !evt.IsDeleted && s.Ticker == ticker
                                select new { Event = evt, Series = ser, Snapshot = s })
                                .FirstOrDefaultAsync(cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("Market {Ticker} not found in database after saving", ticker);
                return null;
            }

            var clientEvent = new ClientEvent
            {
                EventTicker = result.Event.EventTicker,
                SeriesTicker = result.Event.SeriesTicker,
                Title = result.Event.Title,
                SubTitle = result.Event.SubTitle,
                Category = result.Event.Category,

                Ticker = result.Snapshot.Ticker,
                MarketType = result.Snapshot.MarketType,
                YesSubTitle = result.Snapshot.YesSubTitle,
                NoSubTitle = result.Snapshot.NoSubTitle,

                CreatedTime = result.Snapshot.CreatedTime,
                OpenTime = result.Snapshot.OpenTime,
                CloseTime = result.Snapshot.CloseTime,
                ExpectedExpirationTime = result.Snapshot.ExpectedExpirationTime,
                LatestExpirationTime = result.Snapshot.LatestExpirationTime,
                Status = result.Snapshot.Status,

                YesBid = result.Snapshot.YesBid,
                YesBidDollars = result.Snapshot.YesBidDollars,
                YesAsk = result.Snapshot.YesAsk,
                YesAskDollars = result.Snapshot.YesAskDollars,
                NoBid = result.Snapshot.NoBid,
                NoBidDollars = result.Snapshot.NoBidDollars,
                NoAsk = result.Snapshot.NoAsk,
                NoAskDollars = result.Snapshot.NoAskDollars,
                LastPrice = result.Snapshot.LastPrice,
                LastPriceDollars = result.Snapshot.LastPriceDollars,
                PreviousYesBid = result.Snapshot.PreviousYesBid,
                PreviousYesBidDollars = result.Snapshot.PreviousYesBidDollars,
                PreviousYesAsk = result.Snapshot.PreviousYesAsk,
                PreviousYesAskDollars = result.Snapshot.PreviousYesAskDollars,
                PreviousPrice = result.Snapshot.PreviousPrice,
                PreviousPriceDollars = result.Snapshot.PreviousPriceDollars,
                SettlementValue = result.Snapshot.SettlementValue,
                SettlementValueDollars = result.Snapshot.SettlementValueDollars,

                Volume = result.Snapshot.Volume,
                Volume24h = result.Snapshot.Volume24h,
                OpenInterest = result.Snapshot.OpenInterest,
                NotionalValue = result.Snapshot.NotionalValue,
                NotionalValueDollars = result.Snapshot.NotionalValueDollars,

                Liquidity = result.Snapshot.Liquidity,
                LiquidityDollars = result.Snapshot.LiquidityDollars,

                GenerateDate = result.Snapshot.GenerateDate
            };

            return clientEvent;
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error fetching market {Ticker}", ticker);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch market by ticker {Ticker}", ticker);
            throw;
        }
    }

    public async Task<GetEventResponse?> GetEventDetailsAsync(string eventTickerId)
    {
        if (string.IsNullOrWhiteSpace(eventTickerId))
        {
            throw new ArgumentException("EventID is required", nameof(eventTickerId));
        }

        try
        {
            var response = await _kalshiClient.Events.GetEventAsync(eventTickerId, withNestedMarkets: true);

            if (response.Event == null)
            {
                _logger.LogWarning("Market {EventID} was not returned from Kalshi", eventTickerId);
                return null;
            }

            var fetchedAtUtc = DateTime.UtcNow;
            foreach (var market in response.Event.Markets)
            {
                var newSnapshot = MapMarket(market, fetchedAtUtc);
                await _db.MarketSnapshots.AddAsync(newSnapshot);
            }
            await _db.SaveChangesAsync();

            // Insert markets information to MarketSnapshots
            return response;

        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error fetching market {EventTickerId}", eventTickerId);
            throw;
        }
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

    internal static MarketSnapshot MapMarket(Market market, DateTime generateDate)
    {
        return new MarketSnapshot
        {
            Ticker = market.Ticker,
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
