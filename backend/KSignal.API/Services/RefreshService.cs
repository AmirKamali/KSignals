using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KSignal.API.Services;

public class RefreshService
{
    private readonly KalshiClient _kalshiClient;
    private readonly ILogger<RefreshService> _logger;
    private readonly KalshiDbContext _db;

    public RefreshService(KalshiClient kalshiClient, ILogger<RefreshService> logger, KalshiDbContext db)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<int> RefreshMarketCategoriesAsync(string? category = null, string? tag = null, CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requestedCategory = string.IsNullOrWhiteSpace(category) ? null : category;
        var requestedTag = string.IsNullOrWhiteSpace(tag) ? null : tag;

        // Create a queue to process categories and tags
        var queue = new Queue<QueueItem>();

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

            // Add all categories to the queue
            foreach (var cat in tagsResponse.TagsByCategories.Keys)
            {
                queue.Enqueue(new QueueItem { Category = cat, Tag = null, Cursor = null });
            }

            // Add all tags to the queue
            foreach (var kvp in tagsResponse.TagsByCategories)
            {
                if (kvp.Value == null) continue;
                foreach (var t in kvp.Value)
                {
                    queue.Enqueue(new QueueItem { Category = null, Tag = t, Cursor = null });
                }
            }

            // Process queue items one by one
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                _logger.LogInformation("Processing queue item: category={Category}, tag={Tag}, cursor={Cursor}", 
                    item.Category ?? "null", item.Tag ?? "null", item.Cursor ?? "null");

                var cursor = await FetchSeriesIntoLookupAsync(existingIds, seen, now, item.Category, item.Tag, item.Cursor, cancellationToken);

                // If response has cursor, add the same category/tag with cursor to the queue
                if (!string.IsNullOrWhiteSpace(cursor))
                {
                    queue.Enqueue(new QueueItem { Category = item.Category, Tag = item.Tag, Cursor = cursor });
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

            queue.Enqueue(new QueueItem { Category = requestedCategory, Tag = null, Cursor = null });
        }

        if (requestedTag != null)
        {
            var ids = await _db.MarketCategories
                .AsNoTracking()
                .Where(x => x.Tags != null && x.Tags.Contains(requestedTag))
                .Select(x => x.SeriesId)
                .ToListAsync(cancellationToken);
            foreach (var id in ids) existingFiltered.Add(id);

            queue.Enqueue(new QueueItem { Category = null, Tag = requestedTag, Cursor = null });
        }

        // Process queue items one by one
        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            _logger.LogInformation("Processing queue item: category={Category}, tag={Tag}, cursor={Cursor}", 
                item.Category ?? "null", item.Tag ?? "null", item.Cursor ?? "null");

            var cursor = await FetchSeriesIntoLookupAsync(existingFiltered, seen, now, item.Category, item.Tag, item.Cursor, cancellationToken);

            // If response has cursor, add the same category/tag with cursor to the queue
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                queue.Enqueue(new QueueItem { Category = item.Category, Tag = item.Tag, Cursor = cursor });
            }
        }

        await CleanupMissingAsync(existingFiltered, cancellationToken);
        return seen.Count;
    }

    private async Task<string?> FetchSeriesIntoLookupAsync(
        HashSet<string> existingIds,
        HashSet<string> seen,
        DateTime lastUpdate,
        string? category,
        string? tag,
        string? cursor,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use lower-level API client to support cursor pagination
            var requestOptions = new RequestOptions
            {
                Operation = "MarketApi.GetSeriesList"
            };

            if (!string.IsNullOrWhiteSpace(category))
            {
                requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "category", category));
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "tags", tag));
            }

            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "include_product_metadata", false));

            if (!string.IsNullOrWhiteSpace(cursor))
            {
                requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "cursor", cursor));
            }

            var response = await _kalshiClient.Markets.AsynchronousClient.GetAsync<GetSeriesListResponse>(
                    "/series",
                    requestOptions,
                    _kalshiClient.Markets.Configuration,
                    cancellationToken)
                .ConfigureAwait(false);

            var data = response?.Data;
            if (data?.Series == null)
            {
                _logger.LogWarning("Series list response was null (category={Category}, tag={Tag}, cursor={Cursor})", category, tag, cursor);
                return null;
            }

            foreach (var series in data.Series)
            {
                if (series == null || string.IsNullOrWhiteSpace(series.Ticker)) continue;

                var agg = new SeriesAggregation(series.Ticker);
                agg.Merge(series);
                var record = agg.ToRecord(lastUpdate);
                await UpsertSeriesAsync(record, cancellationToken);
                seen.Add(series.Ticker);
                existingIds.Remove(series.Ticker);
            }

            // Check if response has cursor field (may be in the raw response)
            // Since GetSeriesListResponse doesn't have cursor in the model, we'll check the raw response
            string? nextCursor = null;
            if (!string.IsNullOrWhiteSpace(response?.RawContent))
            {
                try
                {
                    var json = JObject.Parse(response.RawContent);
                    nextCursor = json["cursor"]?.ToString();
                }
                catch (Exception ex)
                {
                    // If parsing fails, assume no cursor
                    _logger.LogDebug(ex, "Failed to parse cursor from response (category={Category}, tag={Tag})", category, tag);
                }
            }

            return string.IsNullOrWhiteSpace(nextCursor) ? null : nextCursor;
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error when fetching series (category={Category}, tag={Tag}, cursor={Cursor})", category, tag, cursor);
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
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);

        // Remove MarketCache records where CloseTime is more than 7 days ago
        var oldMarkets = await _db.Markets
            .Where(m => m.CloseTime <= sevenDaysAgo)
            .ToListAsync(cancellationToken);

        if (oldMarkets.Count > 0)
        {
            _logger.LogInformation("Removing {Count} MarketCache records with CloseTime older than 7 days", oldMarkets.Count);
            _db.Markets.RemoveRange(oldMarkets);
            await _db.SaveChangesAsync(cancellationToken);
        }
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

    public async Task<int> CacheMarketDataAsync(string? category = null, string? tag = null, CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);

        // Read `marketcategories` to find series matching the filters and aggregate seriesIds
        var seriesIds = _db.MarketCategories
            .AsNoTracking()
            .Where(mc =>
                (string.IsNullOrWhiteSpace(category) || mc.Category == category) &&
                (string.IsNullOrWhiteSpace(tag) || (mc.Tags != null && mc.Tags.Contains(tag))))
            .Select(mc => mc.SeriesId)
            .Distinct()
            .ToList();

        if (seriesIds == null || seriesIds.Count == 0)
        {
            _logger.LogWarning("No series found for category={Category}, tag={Tag}", category, tag);
            return 0;
        }

        _logger.LogInformation("Caching market data for {SeriesCount} series (category={Category}, tag={Tag})",
            seriesIds.Count, category, tag);

        var nowUtc = DateTime.UtcNow;
        var totalCached = 0;

        // Fetch markets for each series ticker
        foreach (var seriesTicker in seriesIds)
        {
            try
            {
                var marketCount = await FetchAndCacheMarketsForSeriesAsync(seriesTicker, nowUtc, cancellationToken);
                totalCached += marketCount;
                _logger.LogInformation("Cached {Count} markets for series {SeriesTicker}", marketCount, seriesTicker);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching markets for series {SeriesTicker}", seriesTicker);
                // Continue with next series even if one fails
            }
        }

        _logger.LogInformation("Successfully cached {TotalCount} markets across {SeriesCount} series",
            totalCached, seriesIds.Count);

        return totalCached;
    }

    private async Task<int> FetchAndCacheMarketsForSeriesAsync(string seriesTicker, DateTime fetchedAtUtc, CancellationToken cancellationToken)
    {
        var markets = new List<MarketCache>();
        string? cursor = null;

        do
        {
            var requestOptions = new RequestOptions
            {
                Operation = "MarketApi.GetMarkets"
            };

            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "limit", 1000));
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "status", "open"));
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "series_ticker", seriesTicker));

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
                    .Where(m => m != null)
                    .Select(m => KalshiService.MapMarket(seriesTicker, m!, fetchedAtUtc))
                    .ToList();
                markets.AddRange(mapped);
            }

            cursor = data?.Cursor;
        } while (!string.IsNullOrWhiteSpace(cursor));

        // Upsert markets to database
        if (markets.Count > 0)
        {
            await UpsertMarketsAsync(markets, cancellationToken);
        }

        return markets.Count;
    }

    private async Task UpsertMarketsAsync(List<MarketCache> markets, CancellationToken cancellationToken)
    {
        foreach (var market in markets)
        {
            var existing = await _db.Markets.FindAsync(new object[] { market.TickerId }, cancellationToken);
            if (existing == null)
            {
                _db.Markets.Add(market);
            }
            else
            {
                // Update all properties
                existing.SeriesTicker = market.SeriesTicker;
                existing.Title = market.Title;
                existing.Subtitle = market.Subtitle;
                existing.Volume = market.Volume;
                existing.Volume24h = market.Volume24h;
                existing.CreatedTime = market.CreatedTime;
                existing.ExpirationTime = market.ExpirationTime;
                existing.CloseTime = market.CloseTime;
                existing.LatestExpirationTime = market.LatestExpirationTime;
                existing.OpenTime = market.OpenTime;
                existing.Status = market.Status;
                existing.YesBid = market.YesBid;
                existing.YesBidDollars = market.YesBidDollars;
                existing.YesAsk = market.YesAsk;
                existing.YesAskDollars = market.YesAskDollars;
                existing.NoBid = market.NoBid;
                existing.NoBidDollars = market.NoBidDollars;
                existing.NoAsk = market.NoAsk;
                existing.NoAskDollars = market.NoAskDollars;
                existing.LastPrice = market.LastPrice;
                existing.LastPriceDollars = market.LastPriceDollars;
                existing.PreviousYesBid = market.PreviousYesBid;
                existing.PreviousYesBidDollars = market.PreviousYesBidDollars;
                existing.PreviousYesAsk = market.PreviousYesAsk;
                existing.PreviousYesAskDollars = market.PreviousYesAskDollars;
                existing.PreviousPrice = market.PreviousPrice;
                existing.PreviousPriceDollars = market.PreviousPriceDollars;
                existing.Liquidity = market.Liquidity;
                existing.LiquidityDollars = market.LiquidityDollars;
                existing.SettlementValue = market.SettlementValue;
                existing.SettlementValueDollars = market.SettlementValueDollars;
                existing.NotionalValue = market.NotionalValue;
                existing.NotionalValueDollars = market.NotionalValueDollars;
                existing.JsonResponse = market.JsonResponse;
                existing.LastUpdate = market.LastUpdate;

                _db.Markets.Update(existing);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private sealed class QueueItem
    {
        public string? Category { get; set; }
        public string? Tag { get; set; }
        public string? Cursor { get; set; }
    }
}

