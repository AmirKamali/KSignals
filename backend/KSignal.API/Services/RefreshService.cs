using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace KSignal.API.Services;

public class RefreshService
{
    private readonly KalshiClient _kalshiClient;
    private readonly ILogger<RefreshService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    
    // Thread-safe status tracking for cache market data
    private readonly object _statusLock = new object();
    private bool _isCacheMarketDataProcessing = false;
    private int _totalJobs = 0;
    private int _remainingJobs = 0;
    private DateTime? _startedAt;
    private DateTime? _lastUpdatedAt;
    private string? _currentCategory;
    private string? _currentTag;

    // Thread-safe status tracking for category refresh
    private readonly object _categoryRefreshStatusLock = new object();
    private bool _isCategoryRefreshProcessing = false;
    private int _categoryRefreshTotalJobs = 0;
    private int _categoryRefreshRemainingJobs = 0;
    private DateTime? _categoryRefreshStartedAt;
    private DateTime? _categoryRefreshLastUpdatedAt;
    private string? _categoryRefreshCategory;
    private string? _categoryRefreshTag;

    public RefreshService(KalshiClient kalshiClient, ILogger<RefreshService> logger, IServiceScopeFactory scopeFactory)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public async Task GetTodayMarketsAsync(
        int days = 1,
        int maxPages = 5,
        MarketSort sortBy = MarketSort.Volume,
        SortDirection direction = SortDirection.Desc,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var safeDays = Math.Max(1, days);
        var safeMaxPages = Math.Max(1, maxPages);
        var maxCloseTs = DateTimeOffset.UtcNow.AddDays(safeDays).ToUnixTimeSeconds();
        var fetchedAtUtc = nowUtc;
        var results = new List<MarketCache>();
        string? cursor = null;
        var pageCounter = 0;

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
                        return KalshiService.MapMarket(seriesKey, m, fetchedAtUtc);
                    })
                    .ToList();
                results.AddRange(mapped);
            }

            cursor = data?.Cursor;
            pageCounter++;
        } while (!string.IsNullOrWhiteSpace(cursor) && pageCounter < safeMaxPages);

        var ordered = results.OrderBy(m => m.CloseTime).ToList();

        if (results.Count > 0)
        {
            try
            {
                await UpsertMarketsAsync(results, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert {Count} today markets for next {Days} day(s)", results.Count, safeDays);
            }
        }
    }
    
    public CacheMarketStatus GetCacheMarketStatus()
    {
        lock (_statusLock)
        {
            return new CacheMarketStatus
            {
                IsProcessing = _isCacheMarketDataProcessing,
                TotalJobs = _totalJobs,
                RemainingJobs = _remainingJobs,
                StartedAt = _startedAt,
                LastUpdatedAt = _lastUpdatedAt,
                Category = _currentCategory,
                Tag = _currentTag
            };
        }
    }

    public CategoryRefreshStatus GetCategoryRefreshStatus()
    {
        lock (_categoryRefreshStatusLock)
        {
            return new CategoryRefreshStatus
            {
                IsProcessing = _isCategoryRefreshProcessing,
                TotalJobs = _categoryRefreshTotalJobs,
                RemainingJobs = _categoryRefreshRemainingJobs,
                StartedAt = _categoryRefreshStartedAt,
                LastUpdatedAt = _categoryRefreshLastUpdatedAt,
                Category = _categoryRefreshCategory,
                Tag = _categoryRefreshTag
            };
        }
    }

    public bool RefreshMarketCategoriesAsync(string? category = null, string? tag = null)
    {
        lock (_categoryRefreshStatusLock)
        {
            if (_isCategoryRefreshProcessing)
            {
                _logger.LogWarning("Category refresh is already processing. Request ignored.");
                return false;
            }

            _isCategoryRefreshProcessing = true;
            _categoryRefreshStartedAt = DateTime.UtcNow;
            _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
            _categoryRefreshCategory = category;
            _categoryRefreshTag = tag;
            _categoryRefreshTotalJobs = 0;
            _categoryRefreshRemainingJobs = 0;
        }

        // Start background task
        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshMarketCategoriesInternalAsync(category, tag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background category refresh task");
            }
            finally
            {
                lock (_categoryRefreshStatusLock)
                {
                    _isCategoryRefreshProcessing = false;
                    _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
                }
            }
        });

        return true;
    }

    private async Task RefreshMarketCategoriesInternalAsync(string? category = null, string? tag = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KalshiDbContext>();
        
        await db.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requestedCategory = string.IsNullOrWhiteSpace(category) ? null : category;
        var requestedTag = string.IsNullOrWhiteSpace(tag) ? null : tag;

        // Create a queue to process categories and tags
        var queue = new Queue<QueueItem>();

        // Full refresh when no filters are provided
        if (requestedCategory == null && requestedTag == null)
        {
            var existingIds = (await db.MarketCategories
                    .AsNoTracking()
                    .Select(x => x.SeriesId)
                    .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var tagsResponse = await _kalshiClient.Search.GetTagsForSeriesCategoriesAsync();
            if (tagsResponse?.TagsByCategories == null)
            {
                _logger.LogWarning("Tags by categories response was null or empty.");
                lock (_categoryRefreshStatusLock)
                {
                    _categoryRefreshTotalJobs = 0;
                    _categoryRefreshRemainingJobs = 0;
                    _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
                }
                return;
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

            // Convert to ConcurrentQueue for thread-safe parallel processing
            var concurrentQueue = new ConcurrentQueue<QueueItem>();
            foreach (var item in queue)
            {
                concurrentQueue.Enqueue(item);
            }

            // Update status with total jobs
            lock (_categoryRefreshStatusLock)
            {
                _categoryRefreshTotalJobs = concurrentQueue.Count;
                _categoryRefreshRemainingJobs = concurrentQueue.Count;
                _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
            }

            // Process queue items in parallel
            var cancellationTokenSource = new CancellationTokenSource();
            var semaphore = new SemaphoreSlim(10, 10); // Max 10 concurrent operations
            var activeCount = 0;
            var activeCountLock = new object();

            async Task ProcessQueueItemAsync()
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (!concurrentQueue.TryDequeue(out var item))
                    {
                        // Queue is empty, wait a bit and check again
                        await Task.Delay(100, cancellationTokenSource.Token);
                        continue;
                    }

                    Interlocked.Increment(ref activeCount);
                    await semaphore.WaitAsync(cancellationTokenSource.Token);
                    try
                    {
                        var cursor = await FetchSeriesIntoLookupAsync(db, existingIds, seen, now, item.Category, item.Tag, item.Cursor, cancellationTokenSource.Token);

                        // If response has cursor, add the same category/tag with cursor to the queue
                        if (!string.IsNullOrWhiteSpace(cursor))
                        {
                            concurrentQueue.Enqueue(new QueueItem { Category = item.Category, Tag = item.Tag, Cursor = cursor });
                            
                            lock (_categoryRefreshStatusLock)
                            {
                                _categoryRefreshTotalJobs++;
                                _categoryRefreshRemainingJobs++;
                                _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
                            }
                        }

                        // Update remaining jobs
                        lock (_categoryRefreshStatusLock)
                        {
                            _categoryRefreshRemainingJobs = Math.Max(0, _categoryRefreshRemainingJobs - 1);
                            _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing queue item: category={Category}, tag={Tag}, cursor={Cursor}", 
                            item.Category ?? "null", item.Tag ?? "null", item.Cursor ?? "null");
                        
                        // Update remaining jobs even on error
                        lock (_categoryRefreshStatusLock)
                        {
                            _categoryRefreshRemainingJobs = Math.Max(0, _categoryRefreshRemainingJobs - 1);
                            _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                        Interlocked.Decrement(ref activeCount);
                    }
                }
            }

            // Start multiple parallel tasks to process the queue
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(ProcessQueueItemAsync());
            }

            // Wait for queue to be empty and all tasks to finish
            while (concurrentQueue.Count > 0 || Volatile.Read(ref activeCount) > 0)
            {
                await Task.Delay(100, cancellationTokenSource.Token);
            }

            cancellationTokenSource.Cancel();
            await Task.WhenAll(tasks);

            await CleanupMissingAsync(db, existingIds, cancellationTokenSource.Token);
        }
        else
        {
            // Filtered refresh
            var existingFiltered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (requestedCategory != null)
            {
                var ids = await db.MarketCategories
                    .AsNoTracking()
                    .Where(x => x.Category == requestedCategory)
                    .Select(x => x.SeriesId)
                    .ToListAsync();
                foreach (var id in ids) existingFiltered.Add(id);

                queue.Enqueue(new QueueItem { Category = requestedCategory, Tag = null, Cursor = null });
            }

            if (requestedTag != null)
            {
                var ids = await db.MarketCategories
                    .AsNoTracking()
                    .Where(x => x.Tags != null && x.Tags.Contains(requestedTag))
                    .Select(x => x.SeriesId)
                    .ToListAsync();
                foreach (var id in ids) existingFiltered.Add(id);

                queue.Enqueue(new QueueItem { Category = null, Tag = requestedTag, Cursor = null });
            }

            // Convert to ConcurrentQueue for thread-safe parallel processing
            var concurrentQueue = new ConcurrentQueue<QueueItem>();
            foreach (var item in queue)
            {
                concurrentQueue.Enqueue(item);
            }

            // Update status with total jobs
            lock (_categoryRefreshStatusLock)
            {
                _categoryRefreshTotalJobs = concurrentQueue.Count;
                _categoryRefreshRemainingJobs = concurrentQueue.Count;
                _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
            }

            // Process queue items in parallel
            var cancellationTokenSource = new CancellationTokenSource();
            var semaphore = new SemaphoreSlim(10, 10); // Max 10 concurrent operations
            var activeCount = 0;

            async Task ProcessQueueItemAsync()
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (!concurrentQueue.TryDequeue(out var item))
                    {
                        // Queue is empty, wait a bit and check again
                        await Task.Delay(100, cancellationTokenSource.Token);
                        continue;
                    }

                    Interlocked.Increment(ref activeCount);
                    await semaphore.WaitAsync(cancellationTokenSource.Token);
                    try
                    {
                        var cursor = await FetchSeriesIntoLookupAsync(db, existingFiltered, seen, now, item.Category, item.Tag, item.Cursor, cancellationTokenSource.Token);

                        // If response has cursor, add the same category/tag with cursor to the queue
                        if (!string.IsNullOrWhiteSpace(cursor))
                        {
                            concurrentQueue.Enqueue(new QueueItem { Category = item.Category, Tag = item.Tag, Cursor = cursor });
                            
                            lock (_categoryRefreshStatusLock)
                            {
                                _categoryRefreshTotalJobs++;
                                _categoryRefreshRemainingJobs++;
                                _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
                            }
                        }

                        // Update remaining jobs
                        lock (_categoryRefreshStatusLock)
                        {
                            _categoryRefreshRemainingJobs = Math.Max(0, _categoryRefreshRemainingJobs - 1);
                            _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing queue item: category={Category}, tag={Tag}, cursor={Cursor}", 
                            item.Category ?? "null", item.Tag ?? "null", item.Cursor ?? "null");
                        
                        // Update remaining jobs even on error
                        lock (_categoryRefreshStatusLock)
                        {
                            _categoryRefreshRemainingJobs = Math.Max(0, _categoryRefreshRemainingJobs - 1);
                            _categoryRefreshLastUpdatedAt = DateTime.UtcNow;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                        Interlocked.Decrement(ref activeCount);
                    }
                }
            }

            // Start multiple parallel tasks to process the queue
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(ProcessQueueItemAsync());
            }

            // Wait for queue to be empty and all tasks to finish
            while (concurrentQueue.Count > 0 || Volatile.Read(ref activeCount) > 0)
            {
                await Task.Delay(100, cancellationTokenSource.Token);
            }

            cancellationTokenSource.Cancel();
            await Task.WhenAll(tasks);

            await CleanupMissingAsync(db, existingFiltered, cancellationTokenSource.Token);
        }

        _logger.LogInformation("Successfully refreshed {Count} categories/tags", seen.Count);
    }

    private async Task<string?> FetchSeriesIntoLookupAsync(
        KalshiDbContext db,
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
                await UpsertSeriesAsync(db, record, cancellationToken);
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

    private async Task UpsertSeriesAsync(KalshiDbContext db, MarketCategoryRecord record, CancellationToken cancellationToken)
    {
        var existing = await db.MarketCategories.FindAsync(new object[] { record.SeriesId }, cancellationToken);
        if (existing == null)
        {
            db.MarketCategories.Add(new MarketCategory
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

            db.MarketCategories.Update(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task CleanupMissingAsync(KalshiDbContext db, HashSet<string> remainingIds, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);

        // Remove MarketCache records where CloseTime is more than 7 days ago
        var oldMarkets = await db.Markets
            .Where(m => m.CloseTime <= sevenDaysAgo)
            .ToListAsync(cancellationToken);

        if (oldMarkets.Count > 0)
        {
            _logger.LogInformation("Removing {Count} MarketCache records with CloseTime older than 7 days", oldMarkets.Count);
            db.Markets.RemoveRange(oldMarkets);
            await db.SaveChangesAsync(cancellationToken);
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

    public bool CacheMarketDataAsync(string? category = null, string? tag = null)
    {
        lock (_statusLock)
        {
            if (_isCacheMarketDataProcessing)
            {
                _logger.LogWarning("Cache market data is already processing. Request ignored.");
                return false;
            }

            _isCacheMarketDataProcessing = true;
            _startedAt = DateTime.UtcNow;
            _lastUpdatedAt = DateTime.UtcNow;
            _currentCategory = category;
            _currentTag = tag;
            _totalJobs = 0;
            _remainingJobs = 0;
        }

        // Start background task
        _ = Task.Run(async () =>
        {
            try
            {
                await CacheMarketDataInternalAsync(category, tag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background cache market data task");
            }
            finally
            {
                lock (_statusLock)
                {
                    _isCacheMarketDataProcessing = false;
                    _lastUpdatedAt = DateTime.UtcNow;
                }
            }
        });

        return true;
    }

    private async Task CacheMarketDataInternalAsync(string? category = null, string? tag = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KalshiDbContext>();
        
        await db.Database.EnsureCreatedAsync();

        // Read `marketcategories` to find series matching the filters and aggregate seriesIds
        var seriesIds = db.MarketCategories
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
            lock (_statusLock)
            {
                _totalJobs = 0;
                _remainingJobs = 0;
                _lastUpdatedAt = DateTime.UtcNow;
            }
            return;
        }

        _logger.LogInformation("Caching market data for {SeriesCount} series (category={Category}, tag={Tag})",
            seriesIds.Count, category, tag);

        // Update status with total jobs
        lock (_statusLock)
        {
            _totalJobs = seriesIds.Count;
            _remainingJobs = seriesIds.Count;
            _lastUpdatedAt = DateTime.UtcNow;
        }

        var nowUtc = DateTime.UtcNow;
        var totalCached = 0;
        var cancellationTokenSource = new CancellationTokenSource();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 10,
            CancellationToken = cancellationTokenSource.Token
        };

        // Fetch markets for each series ticker in parallel
        await Parallel.ForEachAsync(seriesIds, parallelOptions, async (seriesTicker, ct) =>
        {
            try
            {
                var marketCount = await FetchAndCacheMarketsForSeriesAsync(seriesTicker, nowUtc, ct);
                Interlocked.Add(ref totalCached, marketCount);
                _logger.LogInformation("Cached {Count} markets for series {SeriesTicker}", marketCount, seriesTicker);
                
                // Update remaining jobs
                lock (_statusLock)
                {
                    _remainingJobs = Math.Max(0, _remainingJobs - 1);
                    _lastUpdatedAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching markets for series {SeriesTicker}", seriesTicker);
                // Continue with next series even if one fails
                
                // Update remaining jobs even on error
                lock (_statusLock)
                {
                    _remainingJobs = Math.Max(0, _remainingJobs - 1);
                    _lastUpdatedAt = DateTime.UtcNow;
                }
            }
        });

        _logger.LogInformation("Successfully cached {TotalCount} markets across {SeriesCount} series",
            totalCached, seriesIds.Count);
    }

    private async Task<int> FetchAndCacheMarketsForSeriesAsync(string seriesTicker, DateTime fetchedAtUtc, CancellationToken cancellationToken)
    {
        var markets = new List<MarketCache>();
        string? cursor = null;

        do
        {
            try
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

                // Check if response is successful
                if (response?.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogWarning("Non-OK status code {StatusCode} for series {SeriesTicker}. Response: {RawContent}",
                        response?.StatusCode, seriesTicker, response?.RawContent);
                    break; // Exit pagination loop
                }

                var data = response?.Data;
                if (data?.Markets != null && data.Markets.Count > 0)
                {
                    var mapped = data.Markets
                        .Where(m => m != null)
                        .Select(m => KalshiService.MapMarket(seriesTicker, m!, fetchedAtUtc))
                        .ToList();
                    markets.AddRange(mapped);
                }

                cursor = data?.Cursor;
            }
            catch (ApiException apiEx)
            {
                // Log the API exception with details
                _logger.LogWarning(apiEx, 
                    "API exception for series {SeriesTicker}. ErrorCode: {ErrorCode}, Message: {Message}, ErrorContent: {ErrorContent}",
                    seriesTicker, apiEx.ErrorCode, apiEx.Message, apiEx.ErrorContent);
                
                // If it's a client error (4xx), break the loop as retrying won't help
                // If it's a server error (5xx), we could potentially retry, but for now we'll break
                if (apiEx.ErrorCode >= 400 && apiEx.ErrorCode < 500)
                {
                    _logger.LogInformation("Client error for series {SeriesTicker}, stopping pagination", seriesTicker);
                    break;
                }
                
                // For server errors, we'll break to avoid infinite loops
                // In a production system, you might want to implement retry logic here
                _logger.LogWarning("Server error for series {SeriesTicker}, stopping pagination", seriesTicker);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching markets for series {SeriesTicker}", seriesTicker);
                break; // Exit pagination loop on unexpected errors
            }
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
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KalshiDbContext>();
        
        foreach (var market in markets)
        {
            var existing = await db.Markets.FindAsync(new object[] { market.TickerId }, cancellationToken);
            if (existing == null)
            {
                db.Markets.Add(market);
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

                db.Markets.Update(existing);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed class QueueItem
    {
        public string? Category { get; set; }
        public string? Tag { get; set; }
        public string? Cursor { get; set; }
    }
}
