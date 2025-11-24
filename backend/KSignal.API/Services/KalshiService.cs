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
            throw new InvalidOperationException("Connection string 'KalshiMySql' is missing.");
        }
    }

    public async Task<int> RefreshMarketCategoriesAsync(string? category = null, string? tag = null, CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requestedCategory = string.IsNullOrWhiteSpace(category) ? null : category;
        var requestedTag = string.IsNullOrWhiteSpace(tag) ? null : tag;

        // Full refresh when no filters are provided
        if (requestedCategory == null && requestedTag == null)
        {
            var existingIds = (await _db.MarketCategories
                    .AsNoTracking()
                    .Select(x => x.SeriesId)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var tagsResponse = await _kalshiClient.Search.GetTagsForSeriesCategoriesAsync(cancellationToken: cancellationToken);
            if (tagsResponse?.TagsByCategories == null)
            {
                _logger.LogWarning("Tags by categories response was null or empty.");
                return 0;
            }

            _logger.LogInformation("Fetched {CategoryCount} categories from tags endpoint", tagsResponse.TagsByCategories.Count);

            // Pull series per category
            foreach (var cat in tagsResponse.TagsByCategories.Keys)
            {
                _logger.LogInformation("Fetching series for category {Category}", cat);
                await FetchSeriesIntoLookupAsync(existingIds, seen, now, cat, null, cancellationToken);
            }

            // Pull series per tag
            foreach (var kvp in tagsResponse.TagsByCategories)
            {
                if (kvp.Value == null) continue;
                foreach (var t in kvp.Value)
                {
                    _logger.LogInformation("Fetching series for tag {Tag}", t);
                    await FetchSeriesIntoLookupAsync(existingIds, seen, now, null, t, cancellationToken);
                }
            }

            await CleanupMissingAsync(existingIds, cancellationToken);
            return seen.Count;
        }

        // Filtered refresh
        var existingFiltered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (requestedCategory != null)
        {
            var ids = await _db.MarketCategories
                .AsNoTracking()
                .Where(x => x.Category == requestedCategory)
                .Select(x => x.SeriesId)
                .ToListAsync(cancellationToken);
            foreach (var id in ids) existingFiltered.Add(id);

            _logger.LogInformation("Fetching series for category {Category}", requestedCategory);
            await FetchSeriesIntoLookupAsync(existingFiltered, seen, now, requestedCategory, null, cancellationToken);
        }

        if (requestedTag != null)
        {
            var ids = await _db.MarketCategories
                .AsNoTracking()
                .Where(x => x.Tags != null && x.Tags.Contains(requestedTag))
                .Select(x => x.SeriesId)
                .ToListAsync(cancellationToken);
            foreach (var id in ids) existingFiltered.Add(id);

            _logger.LogInformation("Fetching series for tag {Tag}", requestedTag);
            await FetchSeriesIntoLookupAsync(existingFiltered, seen, now, null, requestedTag, cancellationToken);
        }

        await CleanupMissingAsync(existingFiltered, cancellationToken);
        return seen.Count;
    }

    public async Task<List<MarketCache>> GetMarketsAsync(string? category = null, string? tag = null, string? closeDateType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(tag))
        {
            return await GetTodayMarketsAsync(cancellationToken);
        }

        await _db.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureMarketsTableAsync(cancellationToken);
        var loweredCategory = category?.ToLowerInvariant();
        var loweredTag = tag?.ToLowerInvariant();

        var seriesQuery = _db.MarketCategories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(loweredCategory))
        {
            seriesQuery = seriesQuery.Where(s => s.Category != null && s.Category.ToLower() == loweredCategory);
        }
        if (!string.IsNullOrWhiteSpace(loweredTag))
        {
            seriesQuery = seriesQuery.Where(s => s.Tags != null && s.Tags.ToLower().Contains(loweredTag));
        }

        var seriesTickers = await seriesQuery.Select(s => s.SeriesId).Distinct().ToListAsync(cancellationToken);
        if (seriesTickers.Count == 0) return new List<MarketCache>();

        var nowUtc = DateTime.UtcNow;
        var maxCloseTime = GetMaxCloseTimeFromDateType(closeDateType, nowUtc);

        var query = _db.Markets
            .AsNoTracking()
            .Where(m => seriesTickers.Contains(m.SeriesTicker) && m.CloseTime > nowUtc);

        // Apply date filter if specified
        if (maxCloseTime.HasValue)
        {
            query = query.Where(m => m.CloseTime <= maxCloseTime.Value);
        }

        var cached = await query
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
                LastUpdate = m.LastUpdate
            }).ToListAsync(cancellationToken);

        return cached
            .GroupBy(m => m.SeriesTicker, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Volume24h).First())
            .ToList();
    }

    private static DateTime? GetMaxCloseTimeFromDateType(string? closeDateType, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(closeDateType) || closeDateType.Equals("All time", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var targetDate = nowUtc;

        switch (closeDateType)
        {
            case "Today":
                targetDate = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 23, 59, 59, DateTimeKind.Utc);
                break;

            case "Tomorrow":
                targetDate = nowUtc.AddDays(1);
                targetDate = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 23, 59, 59, DateTimeKind.Utc);
                break;

            case "This week":
                // Find next Sunday (end of week)
                var daysUntilSunday = 7 - (int)nowUtc.DayOfWeek;
                targetDate = nowUtc.AddDays(daysUntilSunday);
                targetDate = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 23, 59, 59, DateTimeKind.Utc);
                break;

            case "This month":
                // Last day of current month
                var lastDayOfMonth = DateTime.DaysInMonth(nowUtc.Year, nowUtc.Month);
                targetDate = new DateTime(nowUtc.Year, nowUtc.Month, lastDayOfMonth, 23, 59, 59, DateTimeKind.Utc);
                break;

            case "Next 3 months":
                targetDate = nowUtc.AddMonths(3);
                targetDate = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 23, 59, 59, DateTimeKind.Utc);
                break;

            case "This year":
                targetDate = new DateTime(nowUtc.Year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                break;

            case "Next year":
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

    private static MarketCache MapMarket(string seriesTicker, Market market, DateTime lastUpdate)
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

    private async Task FetchSeriesIntoLookupAsync(
        HashSet<string> existingIds,
        HashSet<string> seen,
        DateTime lastUpdate,
        string? category,
        string? tag,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kalshiClient.Markets.GetSeriesListAsync(
                category: category,
                tags: tag,
                includeProductMetadata: true,
                cancellationToken: cancellationToken);

            if (response?.Series == null)
            {
                _logger.LogWarning("Series list response was null (category={Category}, tag={Tag})", category, tag);
                return;
            }

            foreach (var series in response.Series)
            {
                if (series == null || string.IsNullOrWhiteSpace(series.Ticker)) continue;

                var agg = new SeriesAggregation(series.Ticker);
                agg.Merge(series);
                var record = agg.ToRecord(lastUpdate);
                await UpsertSeriesAsync(record, cancellationToken);
                seen.Add(series.Ticker);
                existingIds.Remove(series.Ticker);
            }
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error when fetching series (category={Category}, tag={Tag})", category, tag);
            throw;
        }
    }

    private async Task UpsertSeriesAsync(MarketCategoryRecord record, CancellationToken cancellationToken)
    {
        var existing = await _db.MarketCategories.FindAsync(new object[] { record.SeriesId }, cancellationToken);
        if (existing == null)
        {
            _db.MarketCategories.Add(new MarketCategory
            {
                SeriesId = record.SeriesId,
                Category = record.Category,
                Tags = record.Tags,
                Ticker = record.Ticker,
                Title = record.Title,
                Frequency = record.Frequency,
                JsonResponse = record.JsonResponse,
                LastUpdate = record.LastUpdate
            });
        }
        else
        {
            existing.Category = record.Category;
            existing.Tags = record.Tags;
            existing.Ticker = record.Ticker;
            existing.Title = record.Title;
            existing.Frequency = record.Frequency;
            existing.JsonResponse = record.JsonResponse;
            existing.LastUpdate = record.LastUpdate;

            _db.MarketCategories.Update(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task CleanupMissingAsync(HashSet<string> remainingIds, CancellationToken cancellationToken)
    {
        if (remainingIds.Count == 0) return;

        var toRemove = await _db.MarketCategories
            .Where(x => remainingIds.Contains(x.SeriesId))
            .ToListAsync(cancellationToken);

        if (toRemove.Count == 0) return;

        _db.MarketCategories.RemoveRange(toRemove);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private sealed class SeriesAggregation
    {
        public SeriesAggregation(string ticker)
        {
            Ticker = ticker;
        }

        public string Ticker { get; }
        public string? Category { get; private set; }
        public string? Title { get; private set; }
        public string? Frequency { get; private set; }
        public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Series? LatestRaw { get; private set; }

        public void Merge(Series series)
        {
            Category ??= series.Category;
            Title ??= series.Title;
            Frequency ??= series.Frequency;
            foreach (var tag in series.Tags ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    Tags.Add(tag);
                }
            }

            LatestRaw = series;
        }

        public MarketCategoryRecord ToRecord(DateTime lastUpdate)
        {
            var tagsJoined = string.Join(",", Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
            return new MarketCategoryRecord
            {
                SeriesId = Ticker,
                Category = Category,
                Tags = tagsJoined,
                Ticker = Ticker,
                Title = Title,
                Frequency = Frequency,
                JsonResponse = JsonConvert.SerializeObject(LatestRaw),
                LastUpdate = lastUpdate
            };
        }
    }

    private sealed class MarketCategoryRecord
    {
        public string SeriesId { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Tags { get; set; }
        public string? Ticker { get; set; }
        public string? Title { get; set; }
        public string? Frequency { get; set; }
        public string JsonResponse { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
    }
}
