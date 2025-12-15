using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Messaging;
using KSignal.API.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace KSignal.API.Services;

public class SynchronizationService
{
    private readonly KalshiClient _kalshiClient;
    private readonly KalshiDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILockService _lockService;
    private readonly ISyncLogService _syncLogService;

    private const string MarketSyncLockKey = "sync:market-snapshots:lock";
    private const string MarketSyncCounterKey = "sync:market-snapshots:pending";

    public SynchronizationService(
        KalshiClient kalshiClient,
        KalshiDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILockService lockService,
        ISyncLogService syncLogService)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        _syncLogService = syncLogService ?? throw new ArgumentNullException(nameof(syncLogService));
    }

    public async Task EnqueueMarketSyncAsync(
        long? minCreatedTs = null,
        long? maxCreatedTs = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        // Try to acquire the distributed lock to prevent concurrent sync operations
        var lockAcquired = await _lockService.AcquireWithCounterAsync(
            MarketSyncLockKey,
            MarketSyncCounterKey,
            TimeSpan.FromMinutes(30), // Lock expires after 30 minutes as safety fallback
            cancellationToken);

        if (!lockAcquired)
        {
            await _syncLogService.LogSyncEventAsync("MarketSync_FailedToAcquireLock", 0, cancellationToken, LogType.WARN);
            throw new InvalidOperationException("Market synchronization is already in progress. Please wait for the current operation to complete.");
        }

        await _syncLogService.LogSyncEventAsync("MarketSync_LockAcquired", 1, cancellationToken, LogType.Info);

        try
        {
            var messageCount = 0;

            // If any filter is provided, enqueue a single sync message with those filters
            if (minCreatedTs.HasValue || maxCreatedTs.HasValue || !string.IsNullOrWhiteSpace(status))
            {
                await _publishEndpoint.Publish(new SynchronizeMarketData(minCreatedTs, maxCreatedTs, status), cancellationToken);
                messageCount++;
                await _lockService.IncrementJobCounterAsync(MarketSyncCounterKey, cancellationToken);
                await _syncLogService.LogSyncEventAsync("MarketSync_FilteredEnqueued", messageCount, cancellationToken, LogType.Info);
                return;
            }

            // If both min and max are null, we need to perform 2 operations: 1- Get latest data 2- Update existing markets
            var maxCreatedTimeInDb = await _dbContext.MarketSnapshots
                    .AsNoTracking()
                    .MaxAsync(s => (DateTime?)s.CreatedTime, cancellationToken);

            if (!maxCreatedTimeInDb.HasValue)
            {
                // No data in DB, perform full sync from scratch
                await _syncLogService.LogSyncEventAsync("MarketSync_NoExistingData_FullSync", 0, cancellationToken, LogType.Info);
                await _publishEndpoint.Publish(new SynchronizeMarketData(null, null, null), cancellationToken);
                messageCount++;
                await _lockService.IncrementJobCounterAsync(MarketSyncCounterKey, cancellationToken);
                await _syncLogService.LogSyncEventAsync("MarketSync_FullSyncEnqueued", messageCount, cancellationToken, LogType.Info);
                return;
            }
            // Sync1 - Update existing markets (from oldest to newest)
            // Check status of all open markets inside DB and enqueue updates for those
            var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
            var EligibleGenerateDate = DateTime.UtcNow.AddMinutes(-5); // 5 minutes buffer to avoid syncing markets that are still being created
            var diffrentialUpdateStart = await _dbContext.MarketSnapshotsLatestView
                    .AsNoTracking()
                    .Where(m => m.Status == "Active" && m.CloseTime >= twoDaysAgo && m.GenerateDate <= EligibleGenerateDate)
                    .OrderBy(p => p.GenerateDate)
                    .MinAsync(p => (DateTime?)p.CloseTime);

            // Convert DateTime to Unix timestamp (long)
            long? minCreatedTsValue = diffrentialUpdateStart.HasValue
                ? new DateTimeOffset(diffrentialUpdateStart.Value, TimeSpan.Zero).ToUnixTimeSeconds()
                : null;
            if (!minCreatedTsValue.HasValue || minCreatedTsValue == 0)
            {
                return;
            }

            await _publishEndpoint.Publish(new SynchronizeMarketData(minCreatedTsValue, null, null));
            messageCount++;
            await _lockService.IncrementJobCounterAsync(MarketSyncCounterKey, cancellationToken);
            await _syncLogService.LogSyncEventAsync("MarketSync_Enqueued", messageCount, cancellationToken, LogType.Info);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"MarketSync_EnqueueError_{ex.GetType().Name}", 0, cancellationToken, LogType.ERROR);

            // Release the lock and reset counter on failure
            await _lockService.ReleaseWithCounterAsync(MarketSyncLockKey, MarketSyncCounterKey, cancellationToken);

            throw;
        }
    }

    public async Task SynchronizeMarketDataAsync(SynchronizeMarketData command, CancellationToken cancellationToken)
    {
        // If a specific market ticker is provided, sync only that market
        if (!string.IsNullOrWhiteSpace(command.MarketTickerId))
        {
            await SynchronizeSingleMarketAsync(command.MarketTickerId, cancellationToken);
            return;
        }

        // Sync markets with filters
        var request = BuildRequest(command.MinCreatedTs, command.MaxCreatedTs, command.Status, command.Cursor);
        var response = await _kalshiClient.Markets.GetMarketsAsync(limit: 500, cursor: command.Cursor,
        minCreatedTs: command.MinCreatedTs,
         maxCreatedTs: command.MaxCreatedTs,

         status: command.Status);


        var fetchedAt = DateTime.UtcNow;

        var mapped = response.Markets?
            .Where(m => m != null)
            .Select(m =>
            {
                return KalshiService.MapMarket(m, fetchedAt);
            })
            .ToList() ?? new List<MarketSnapshot>();

        await InsertMarketSnapshotsAsync(mapped);

        // If cursor exists, enqueue next page with same parameters + cursor
        if (!string.IsNullOrWhiteSpace(response.Cursor))
        {
            await _publishEndpoint.Publish(
                new SynchronizeMarketData(command.MinCreatedTs, command.MaxCreatedTs, command.Status, response.Cursor),
                cancellationToken);
            // Track the pagination subjob
            await _lockService.IncrementJobCounterAsync(MarketSyncCounterKey, cancellationToken);
            await _syncLogService.LogSyncEventAsync("MarketSync_PaginationEnqueued", 1, cancellationToken, LogType.DEBUG);
        }
        else
        {
            await _syncLogService.LogSyncEventAsync("MarketSync_Completed", 1, cancellationToken, LogType.Info);
        }
    }

    private async Task SynchronizeSingleMarketAsync(string marketTickerId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kalshiClient.Markets.GetMarketAsync(marketTickerId, cancellationToken: cancellationToken);
            var market = response?.Market;

            if (market == null)
            {
                await _syncLogService.LogSyncEventAsync($"MarketSync_SingleMarket_NotFound_{marketTickerId}", 0, cancellationToken, LogType.WARN);
                return;
            }

            var fetchedAt = DateTime.UtcNow;
            var mapped = KalshiService.MapMarket(market, fetchedAt);

            await InsertMarketSnapshotsAsync(new[] { mapped });
        }
        catch (ApiException apiEx)
        {
            await _syncLogService.LogSyncEventAsync($"MarketSync_SingleMarket_ApiError_{marketTickerId}", 0, cancellationToken, LogType.ERROR);
            throw;
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"MarketSync_SingleMarket_Error_{marketTickerId}", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    private static RequestOptions BuildRequest(long? minCreatedTs, long? maxCreatedTs, string? status, string? cursor)
    {
        var requestOptions = new RequestOptions
        {
            Operation = "MarketApi.GetMarkets"
        };

        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "limit", 250));
        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "with_nested_markets", true));

        if (minCreatedTs.HasValue)
        {
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "min_created_ts", minCreatedTs.Value));
        }

        if (maxCreatedTs.HasValue)
        {
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "max_created_ts", maxCreatedTs.Value));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "status", status));
        }

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "cursor", cursor));
        }

        return requestOptions;
    }

    public async Task EnqueueTagsCategoriesSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new SynchronizeTagsCategories(), cancellationToken);
            await _syncLogService.LogSyncEventAsync("SynchronizeTagsCategories", 1, cancellationToken);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"TagsCategoriesSync_EnqueueError_{ex.GetType().Name}", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    public async Task SynchronizeTagsCategoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _syncLogService.LogSyncEventAsync("TagsCategoriesSync_Fetching", 0, cancellationToken, LogType.Info);

            var response = await _kalshiClient.Search.GetTagsForSeriesCategoriesAsync(cancellationToken: cancellationToken);

            if (response?.TagsByCategories == null || response.TagsByCategories.Count == 0)
            {
                await _syncLogService.LogSyncEventAsync("TagsCategoriesSync_NoDataReceived", 0, cancellationToken, LogType.WARN);
                return;
            }

            var now = DateTime.UtcNow;

            // Build a set of incoming (Category, Tag) pairs for quick lookup
            var incomingKeys = new HashSet<(string Category, string Tag)>();
            var incomingRecords = new List<TagsCategory>();

            foreach (var kvp in response.TagsByCategories)
            {
                var category = kvp.Key;
                var tags = kvp.Value ?? new List<string>();

                foreach (var tag in tags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                        continue;

                    var key = (category, tag);
                    incomingKeys.Add(key);
                    incomingRecords.Add(new TagsCategory
                    {
                        Category = category,
                        Tag = tag,
                        LastUpdate = now,
                        IsDeleted = false
                    });
                }
            }

            if (incomingRecords.Count == 0)
            {
                await _syncLogService.LogSyncEventAsync("TagsCategoriesSync_NoValidData", 0, cancellationToken, LogType.WARN);
                return;
            }

            // Load all existing records
            var existingRecords = await _dbContext.TagsCategories
                .ToListAsync(cancellationToken);

            var existingDict = existingRecords
                .ToDictionary(e => (e.Category, e.Tag), e => e);

            // Get max ID for generating new IDs (ClickHouse doesn't support auto-increment)
            var nextId = existingRecords.Count > 0 ? existingRecords.Max(e => e.Id) + 1 : 1;

            var updatedCount = 0;
            var insertedCount = 0;
            var deletedCount = 0;
            var restoredCount = 0;

            // Process incoming records: upsert logic
            foreach (var incoming in incomingRecords)
            {
                var key = (incoming.Category, incoming.Tag);

                if (existingDict.TryGetValue(key, out var existing))
                {
                    // Record exists - update it
                    if (existing.IsDeleted)
                    {
                        // Restore previously deleted record
                        existing.IsDeleted = false;
                        existing.LastUpdate = now;
                        restoredCount++;
                    }
                    else
                    {
                        // Just update the timestamp
                        existing.LastUpdate = now;
                    }
                    updatedCount++;
                }
                else
                {
                    // New record - assign ID and insert it
                    incoming.Id = nextId++;
                    await _dbContext.TagsCategories.AddAsync(incoming, cancellationToken);
                    insertedCount++;
                }
            }

            // Mark records as deleted if they're not in incoming data
            foreach (var existing in existingRecords)
            {
                var key = (existing.Category, existing.Tag);

                if (!incomingKeys.Contains(key) && !existing.IsDeleted)
                {
                    existing.IsDeleted = true;
                    existing.LastUpdate = now;
                    deletedCount++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _syncLogService.LogSyncEventAsync(
                "TagsCategoriesSync_Completed",
                insertedCount + updatedCount + restoredCount + deletedCount,
                cancellationToken,
                LogType.Info);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync("TagsCategoriesSync_Error", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    private async Task InsertMarketSnapshotsAsync(IReadOnlyCollection<MarketSnapshot> markets)
    {
        try
        {
            if (markets.Count == 0)
            {
                return;
            }

            // MarketSnapshotID is auto-generated by ClickHouse via generateUUIDv4()
            await _dbContext.MarketSnapshots.AddRangeAsync(markets);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"MarketSnapshots_InsertError_BatchSize{markets.Count}", 0, default, LogType.ERROR);
            throw;
        }
    }

    public async Task EnqueueSeriesSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new SynchronizeSeries(), cancellationToken);
            await _syncLogService.LogSyncEventAsync("SynchronizeSeries", 1, cancellationToken);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"SeriesSync_EnqueueError_{ex.GetType().Name}", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    public async Task SynchronizeSeriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _syncLogService.LogSyncEventAsync("SeriesSync_Fetching", 0, cancellationToken, LogType.Info);

            var response = await _kalshiClient.Markets.GetSeriesListAsync(
                includeProductMetadata: true,
                cancellationToken: cancellationToken);

            if (response?.Series == null || response.Series.Count == 0)
            {
                await _syncLogService.LogSyncEventAsync("SeriesSync_NoDataReceived", 0, cancellationToken, LogType.WARN);
                return;
            }

            var now = DateTime.UtcNow;

            // Build a set of incoming ticker keys for quick lookup
            var incomingTickers = new HashSet<string>();
            var incomingRecords = new List<MarketSeries>();

            foreach (var series in response.Series)
            {
                if (string.IsNullOrWhiteSpace(series.Ticker))
                    continue;

                incomingTickers.Add(series.Ticker);
                incomingRecords.Add(MapSeriesToMarketSeries(series, now));
            }

            if (incomingRecords.Count == 0)
            {
                await _syncLogService.LogSyncEventAsync("SeriesSync_NoValidData", 0, cancellationToken, LogType.WARN);
                return;
            }

            // Load existing tickers to track deleted ones (using AsNoTracking to avoid EF updates)
            var existingTickers = await _dbContext.MarketSeries
                .AsNoTracking()
                .Select(e => new { e.Ticker, e.IsDeleted })
                .ToListAsync(cancellationToken);

            var existingTickerSet = existingTickers
                .Select(e => e.Ticker)
                .ToHashSet();

            var insertedCount = incomingRecords.Count;
            var deletedCount = 0;

            // For ClickHouse ReplacingMergeTree, we always INSERT new rows.
            // The engine will keep only the row with the latest LastUpdate after background merges.
            await _dbContext.MarketSeries.AddRangeAsync(incomingRecords, cancellationToken);

            // Mark records as deleted if they're not in incoming data
            // Insert new rows with IsDeleted = true for soft delete
            var tickersToDelete = existingTickers
                .Where(e => !incomingTickers.Contains(e.Ticker) && !e.IsDeleted)
                .Select(e => e.Ticker)
                .ToList();

            foreach (var ticker in tickersToDelete)
            {
                var deletedRecord = new MarketSeries
                {
                    Ticker = ticker,
                    Frequency = string.Empty,
                    Title = string.Empty,
                    Category = string.Empty,
                    LastUpdate = now,
                    IsDeleted = true
                };
                await _dbContext.MarketSeries.AddAsync(deletedRecord, cancellationToken);
                deletedCount++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _syncLogService.LogSyncEventAsync(
                "SeriesSync_Completed",
                insertedCount + deletedCount,
                cancellationToken,
                LogType.Info);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync("SeriesSync_Error", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    private static MarketSeries MapSeriesToMarketSeries(Series series, DateTime timestamp)
    {
        return new MarketSeries
        {
            Ticker = series.Ticker,
            Frequency = series.Frequency ?? string.Empty,
            Title = series.Title ?? string.Empty,
            Category = series.Category ?? string.Empty,
            Tags = series.Tags != null ? JsonConvert.SerializeObject(series.Tags) : null,
            SettlementSources = series.SettlementSources != null
                ? JsonConvert.SerializeObject(series.SettlementSources.Select(s => new { name = s.Name, url = s.Url }))
                : null,
            ContractUrl = series.ContractUrl,
            ContractTermsUrl = series.ContractTermsUrl,
            ProductMetadata = series.ProductMetadata != null ? JsonConvert.SerializeObject(series.ProductMetadata) : null,
            FeeType = series.FeeType.ToString().ToLowerInvariant(),
            FeeMultiplier = series.FeeMultiplier,
            AdditionalProhibitions = series.AdditionalProhibitions != null
                ? JsonConvert.SerializeObject(series.AdditionalProhibitions)
                : null,
            LastUpdate = timestamp,
            IsDeleted = false
        };
    }

    public async Task EnqueueEventsSyncAsync(string? cursor = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var enqueuedCount = 0;

            if (!String.IsNullOrEmpty(cursor))
            {
                // Always enqueue the bulk events sync (paginated)
                await _publishEndpoint.Publish(new SynchronizeEvents(cursor), cancellationToken);
                enqueuedCount++;
            }

            // If events table is not empty, find missing events from market snapshots
            var hasEvents = await _dbContext.MarketEvents.AnyAsync(cancellationToken);
            if (hasEvents)
            {
                await GetMissingEventDetailsFromNewMarketsAsync();
                // Note: GetMissingEventDetailsFromNewMarketsAsync enqueues messages internally
                // We'll log those separately
            }
            else
            {
                // Table is empty and no cursor provided, lets refresh from scratch
                await _publishEndpoint.Publish(new SynchronizeEvents(cursor), cancellationToken);
                enqueuedCount++;
            }

            if (enqueuedCount > 0)
            {
                await _syncLogService.LogSyncEventAsync("SynchronizeEvents", enqueuedCount, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"EventsSync_EnqueueError_{ex.GetType().Name}", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    private async Task GetMissingEventDetailsFromNewMarketsAsync()
    {
        // Get distinct EventTickers from market_snapshots that don't exist in market_events
        // Using raw SQL with LEFT ANTI JOIN - optimized for ClickHouse's columnar engine
        var missingEventTickers = new List<string>();
        var connection = _dbContext.Database.GetDbConnection();
        var connectionWasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!connectionWasOpen)
        {
            await connection.OpenAsync();
        }
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                        SELECT DISTINCT ms.EventTicker 
                        FROM kalshi_signals.market_snapshots ms
                        LEFT ANTI JOIN kalshi_signals.market_events me ON ms.EventTicker = me.EventTicker
                        WHERE ms.EventTicker != ''";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                missingEventTickers.Add(reader.GetString(0));
            }
        }
        finally
        {
            if (!connectionWasOpen)
            {
                await connection.CloseAsync();
            }
        }

        if (missingEventTickers.Count > 0)
        {
            await _syncLogService.LogSyncEventAsync("EventsSync_MissingEventsFound", missingEventTickers.Count, default, LogType.Info);

            // Batch publish for better throughput
            var messages = missingEventTickers.Select(ticker => new SynchronizeEventDetail(ticker));
            await _publishEndpoint.PublishBatch(messages);

            // Log the sync event
            await _syncLogService.LogSyncEventAsync("SynchronizeEventDetail", missingEventTickers.Count);
        }
    }

    public async Task EnqueueEventDetailSyncAsync(string eventTicker, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eventTicker))
            {
                throw new ArgumentException("Event ticker is required", nameof(eventTicker));
            }
            await _publishEndpoint.Publish(new SynchronizeEventDetail(eventTicker), cancellationToken);
            await _syncLogService.LogSyncEventAsync("SynchronizeEventDetail", 1, cancellationToken);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"EventDetailSync_EnqueueError_{eventTicker}_{ex.GetType().Name}", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    public async Task SynchronizeEventDetailAsync(SynchronizeEventDetail command, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kalshiClient.Events.GetEventAsync(
                command.EventTicker,
                withNestedMarkets: false,
                cancellationToken: cancellationToken);

            if (response?.Event == null)
            {
                await _syncLogService.LogSyncEventAsync($"EventDetailSync_NoData_{command.EventTicker}", 0, cancellationToken, LogType.WARN);
                return;
            }

            var eventData = response.Event;
            var now = DateTime.UtcNow;

            // Check if event already exists (using AsNoTracking to avoid EF change tracking)
            var existingEvent = await _dbContext.MarketEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventTicker == eventData.EventTicker, cancellationToken);

            // For ClickHouse ReplacingMergeTree, we always INSERT a new row.
            // The engine will keep only the row with the latest LastUpdate after background merges.
            var newEvent = MapEventDataToMarketEvent(eventData, now);
            await _dbContext.MarketEvents.AddAsync(newEvent, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (ApiException apiEx) when (apiEx.Message.Contains("not found") ||
                                          apiEx.Message.Contains("Required property") ||
                                          apiEx.Message.Contains("404"))
        {
            // Event doesn't exist or API returned unexpected response format - skip gracefully
            await _syncLogService.LogSyncEventAsync($"EventDetailSync_NotFound_{command.EventTicker}", 0, cancellationToken, LogType.WARN);
        }
        catch (ApiException apiEx) when (apiEx.Message.Contains("too_many_requests"))
        {
            // Rate limited - log and don't crash, let the message be retried
            await _syncLogService.LogSyncEventAsync($"EventDetailSync_RateLimited_{command.EventTicker}", 0, cancellationToken, LogType.WARN);
            throw; // Rethrow to trigger retry
        }
        catch (ApiException apiEx)
        {
            await _syncLogService.LogSyncEventAsync($"EventDetailSync_ApiError_{command.EventTicker}", 0, cancellationToken, LogType.ERROR);
            // Don't rethrow - allow batch to continue processing other messages
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"EventDetailSync_Error_{command.EventTicker}", 0, cancellationToken, LogType.ERROR);
            // Don't rethrow - allow batch to continue processing other messages
        }
    }

    public async Task SynchronizeEventsAsync(SynchronizeEvents command, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kalshiClient.Events.GetEventsAsync(
                limit: 200,
                cursor: command.Cursor,
                withNestedMarkets: false,
                cancellationToken: cancellationToken);

            if (response?.Events == null || response.Events.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var eventsToInsert = new List<MarketEvent>();

            // For ClickHouse ReplacingMergeTree, we always INSERT new rows.
            // The engine will keep only the row with the latest LastUpdate after background merges.
            foreach (var eventData in response.Events)
            {
                if (string.IsNullOrWhiteSpace(eventData.EventTicker))
                    continue;

                eventsToInsert.Add(MapEventDataToMarketEvent(eventData, now));
            }

            if (eventsToInsert.Count > 0)
            {
                await _dbContext.MarketEvents.AddRangeAsync(eventsToInsert, cancellationToken);
                await _syncLogService.LogSyncEventAsync("EventsSync_Inserting", eventsToInsert.Count, cancellationToken, LogType.Info);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Queue next page if there's more data, preserving MinCloseTs
            if (!string.IsNullOrWhiteSpace(response.Cursor))
            {
                await _publishEndpoint.Publish(new SynchronizeEvents(response.Cursor), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync("EventsSync_Error", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    private static MarketEvent MapEventDataToMarketEvent(EventData eventData, DateTime timestamp)
    {
        return new MarketEvent
        {
            EventTicker = eventData.EventTicker,
            SeriesTicker = eventData.SeriesTicker,
            SubTitle = eventData.SubTitle,
            Title = eventData.Title,
            CollateralReturnType = eventData.CollateralReturnType,
            MutuallyExclusive = eventData.MutuallyExclusive,
            Category = eventData.Category,
            StrikeDate = eventData.StrikeDate,
            StrikePeriod = eventData.StrikePeriod,
            AvailableOnBrokers = eventData.AvailableOnBrokers,
            ProductMetadata = eventData.ProductMetadata != null
                ? JsonConvert.SerializeObject(eventData.ProductMetadata)
                : null,
            LastUpdate = timestamp,
            IsDeleted = false
        };
    }

    public async Task EnqueueOrderbookSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new SynchronizeOrderbook(), cancellationToken);
            await _syncLogService.LogSyncEventAsync("SynchronizeOrderbook", 1, cancellationToken);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"OrderbookSync_EnqueueError_{ex.GetType().Name}", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    public async Task SynchronizeOrderbooksAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get top 5 high-priority markets ordered by priority (descending)
            // Use AsNoTracking since we only read these records
            var highPriorityMarkets = await _dbContext.MarketHighPriorities
                .AsNoTracking()
                .OrderByDescending(m => m.Priority)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (highPriorityMarkets.Count == 0)
            {
                await _syncLogService.LogSyncEventAsync("OrderbookSync_NoHighPriorityMarkets", 0, cancellationToken, LogType.WARN);
                return;
            }

            await _syncLogService.LogSyncEventAsync("OrderbookSync_Starting", highPriorityMarkets.Count, cancellationToken, LogType.Info);

            var now = DateTime.UtcNow;
            var snapshotsToInsert = new List<OrderbookSnapshot>();
            var eventsToInsert = new List<OrderbookEvent>();

            // IDs are auto-generated by ClickHouse via generateUUIDv4()

            foreach (var market in highPriorityMarkets)
            {
                try
                {
                    await _syncLogService.LogSyncEventAsync($"OrderbookSync_Fetching_{market.TickerId}", 0, cancellationToken, LogType.DEBUG);

                    var response = await _kalshiClient.Markets.GetMarketOrderbookAsync(
                        market.TickerId,
                        depth: 0, // Get all levels
                        cancellationToken: cancellationToken);

                    if (response?.Orderbook == null || (response.Orderbook.Yes == null && response.Orderbook.No == null))
                    {
                        await _syncLogService.LogSyncEventAsync($"OrderbookSync_NoData_{market.TickerId}", 0, cancellationToken, LogType.WARN);
                        continue;
                    }

                    var orderbook = response.Orderbook;

                    // Handle null arrays from API (empty orderbook)
                    var yesLevels = orderbook.Yes ?? new List<List<decimal>>();
                    var noLevels = orderbook.No ?? new List<List<decimal>>();
                    var yesDollars = orderbook.YesDollars ?? new List<List<string>>();
                    var noDollars = orderbook.NoDollars ?? new List<List<string>>();

                    // Get the previous snapshot to compute events
                    var previousSnapshot = await _dbContext.OrderbookSnapshots
                        .Where(s => s.MarketId == market.TickerId)
                        .OrderByDescending(s => s.CapturedAt)
                        .FirstOrDefaultAsync(cancellationToken);

                    // Calculate metrics
                    var bestYes = yesLevels.Count > 0 ? (double)yesLevels[0][0] : (double?)null;
                    var bestNo = noLevels.Count > 0 ? (double)noLevels[0][0] : (double?)null;
                    var spread = (bestYes.HasValue && bestNo.HasValue) ? 100 - bestYes.Value - bestNo.Value : (double?)null;
                    var totalYesLiquidity = yesLevels.Sum(level => (double)level[1]);
                    var totalNoLiquidity = noLevels.Sum(level => (double)level[1]);

                    // Create snapshot (Id is auto-generated by ClickHouse)
                    var snapshot = new OrderbookSnapshot
                    {
                        MarketId = market.TickerId,
                        CapturedAt = now,
                        YesLevels = JsonConvert.SerializeObject(yesLevels),
                        NoLevels = JsonConvert.SerializeObject(noLevels),
                        YesDollars = JsonConvert.SerializeObject(yesDollars),
                        NoDollars = JsonConvert.SerializeObject(noDollars),
                        BestYes = bestYes,
                        BestNo = bestNo,
                        Spread = spread,
                        TotalYesLiquidity = totalYesLiquidity,
                        TotalNoLiquidity = totalNoLiquidity
                    };
                    snapshotsToInsert.Add(snapshot);

                    // Compute orderbook events by comparing with previous snapshot
                    // For first snapshot, generate "add" events for all levels
                    var events = ComputeOrderbookEvents(
                        market.TickerId,
                        previousSnapshot, // null for first snapshot
                        yesLevels,
                        noLevels,
                        now);
                    eventsToInsert.AddRange(events);

                    // Note: We don't update LastUpdate on market_highpriority here because
                    // ClickHouse ReplacingMergeTree uses LastUpdate as the version column
                    // and doesn't allow direct updates. The sync time can be tracked via
                    // orderbook_snapshots.CapturedAt instead.
                }
                catch (Exception ex)
                {
                    await _syncLogService.LogSyncEventAsync($"OrderbookSync_FetchError_{market.TickerId}", 0, cancellationToken, LogType.ERROR);
                }
            }

            // Save all snapshots and events
            if (snapshotsToInsert.Count > 0)
            {
                await _dbContext.OrderbookSnapshots.AddRangeAsync(snapshotsToInsert, cancellationToken);
            }
            if (eventsToInsert.Count > 0)
            {
                await _dbContext.OrderbookEvents.AddRangeAsync(eventsToInsert, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _syncLogService.LogSyncEventAsync("OrderbookSync_Completed", snapshotsToInsert.Count + eventsToInsert.Count, cancellationToken, LogType.Info);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync("OrderbookSync_Error", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    /// <summary>
    /// Computes incremental orderbook events by diffing previous snapshot with current orderbook.
    /// 
    /// Event types:
    /// - add: A new price level appears (previously no orders at this price, now size > 0)
    /// - update: A price level's total size changes (was > 0, still > 0 but different)
    /// - remove: A price level disappears (was > 0, now 0 or missing)
    /// 
    /// Note: Size field represents the TOTAL size at that price level after the event (not delta).
    /// This allows simple replay: just set book[side, price] = size (or remove if size == 0).
    /// 
    /// When previousSnapshot is null (first sync), all current levels are treated as "add" events.
    /// </summary>
    private static List<OrderbookEvent> ComputeOrderbookEvents(
        string marketId,
        OrderbookSnapshot? previousSnapshot,
        List<List<decimal>> currentYesLevels,
        List<List<decimal>> currentNoLevels,
        DateTime eventTime)
    {
        // Note: Id is auto-generated by ClickHouse via generateUUIDv4()
        var events = new List<OrderbookEvent>();

        // Parse previous levels from JSON (empty if first snapshot)
        var prevYes = previousSnapshot != null && !string.IsNullOrEmpty(previousSnapshot.YesLevels)
            ? JsonConvert.DeserializeObject<List<List<decimal>>>(previousSnapshot.YesLevels) ?? new List<List<decimal>>()
            : new List<List<decimal>>();
        var prevNo = previousSnapshot != null && !string.IsNullOrEmpty(previousSnapshot.NoLevels)
            ? JsonConvert.DeserializeObject<List<List<decimal>>>(previousSnapshot.NoLevels) ?? new List<List<decimal>>()
            : new List<List<decimal>>();

        // Convert to dictionaries for comparison (price -> size)
        var prevYesDict = prevYes.ToDictionary(l => l[0], l => l[1]);
        var prevNoDict = prevNo.ToDictionary(l => l[0], l => l[1]);
        var currYesDict = currentYesLevels.ToDictionary(l => l[0], l => l[1]);
        var currNoDict = currentNoLevels.ToDictionary(l => l[0], l => l[1]);

        // Compare YES side - detect ADD and UPDATE events
        foreach (var (price, size) in currYesDict)
        {
            if (!prevYesDict.TryGetValue(price, out var prevSize) || prevSize == 0)
            {
                // ADD: Previously no orders at this price, now there are
                events.Add(new OrderbookEvent
                {
                    MarketId = marketId,
                    EventTime = eventTime,
                    Side = "YES",
                    Price = (double)price,
                    Size = (double)size, // Total size after event
                    EventType = "add"
                });
            }
            else if (size != prevSize)
            {
                // UPDATE: Price level still exists but size changed
                events.Add(new OrderbookEvent
                {
                    MarketId = marketId,
                    EventTime = eventTime,
                    Side = "YES",
                    Price = (double)price,
                    Size = (double)size, // Total size after event (NOT delta)
                    EventType = "update"
                });
            }
        }

        // Compare YES side - detect REMOVE events
        foreach (var (price, prevSize) in prevYesDict)
        {
            if (prevSize > 0 && (!currYesDict.TryGetValue(price, out var currSize) || currSize == 0))
            {
                // REMOVE: Price level disappeared
                events.Add(new OrderbookEvent
                {
                    MarketId = marketId,
                    EventTime = eventTime,
                    Side = "YES",
                    Price = (double)price,
                    Size = 0, // Explicit zero for remove
                    EventType = "remove"
                });
            }
        }

        // Compare NO side - detect ADD and UPDATE events
        foreach (var (price, size) in currNoDict)
        {
            if (!prevNoDict.TryGetValue(price, out var prevSize) || prevSize == 0)
            {
                // ADD: Previously no orders at this price, now there are
                events.Add(new OrderbookEvent
                {
                    MarketId = marketId,
                    EventTime = eventTime,
                    Side = "NO",
                    Price = (double)price,
                    Size = (double)size, // Total size after event
                    EventType = "add"
                });
            }
            else if (size != prevSize)
            {
                // UPDATE: Price level still exists but size changed
                events.Add(new OrderbookEvent
                {
                    MarketId = marketId,
                    EventTime = eventTime,
                    Side = "NO",
                    Price = (double)price,
                    Size = (double)size, // Total size after event (NOT delta)
                    EventType = "update"
                });
            }
        }

        // Compare NO side - detect REMOVE events
        foreach (var (price, prevSize) in prevNoDict)
        {
            if (prevSize > 0 && (!currNoDict.TryGetValue(price, out var currSize) || currSize == 0))
            {
                // REMOVE: Price level disappeared
                events.Add(new OrderbookEvent
                {
                    MarketId = marketId,
                    EventTime = eventTime,
                    Side = "NO",
                    Price = (double)price,
                    Size = 0, // Explicit zero for remove
                    EventType = "remove"
                });
            }
        }

        return events;
    }

    public async Task EnqueueCandlesticksSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _publishEndpoint.Publish(new SynchronizeCandlesticks(), cancellationToken);
            await _syncLogService.LogSyncEventAsync("SynchronizeCandlesticks", 1, cancellationToken);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync($"CandlesticksSync_EnqueueError_{ex.GetType().Name}", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    public async Task SynchronizeCandlesticksAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get markets with FetchCandlesticks = true from market_highpriority
            var highPriorityMarkets = await _dbContext.MarketHighPriorities
                .AsNoTracking()
                .Where(m => m.FetchCandlesticks)
                .OrderByDescending(m => m.Priority)
                .ToListAsync(cancellationToken);

            if (highPriorityMarkets.Count == 0)
            {
                await _syncLogService.LogSyncEventAsync("CandlesticksSync_NoHighPriorityMarkets", 0, cancellationToken, LogType.WARN);
                return;
            }

            await _syncLogService.LogSyncEventAsync("CandlesticksSync_Starting", highPriorityMarkets.Count, cancellationToken, LogType.Info);

            var now = DateTime.UtcNow;
            var candlesticksToInsert = new List<MarketCandlestickData>();

            // End time is always now
            var endTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            foreach (var market in highPriorityMarkets)
            {
                try
                {
                    // Find the market snapshot and event to get SeriesTicker and CreatedTime
                    var snapshot = await _dbContext.MarketSnapshots
                        .AsNoTracking()
                        .Where(s => s.Ticker == market.TickerId && s.Status == "Active")
                        .OrderByDescending(s => s.GenerateDate)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (snapshot == null)
                    {
                        await _syncLogService.LogSyncEventAsync($"CandlesticksSync_NoSnapshot_{market.TickerId}", 0, cancellationToken, LogType.WARN);
                        continue;
                    }

                    // Look up the seriesTicker from market_events table using EventTicker
                    var seriesTicker = string.Empty;
                    if (!string.IsNullOrWhiteSpace(snapshot.EventTicker))
                    {
                        var marketEvent = await _dbContext.MarketEvents
                            .AsNoTracking()
                            .Where(e => e.EventTicker == snapshot.EventTicker)
                            .FirstOrDefaultAsync(cancellationToken);
                        seriesTicker = marketEvent?.SeriesTicker ?? string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(seriesTicker))
                    {
                        await _syncLogService.LogSyncEventAsync($"CandlesticksSync_NoSeriesTicker_{market.TickerId}", 0, cancellationToken, LogType.WARN);
                        continue;
                    }

                    // Determine start time: last synced candlestick or market CreatedTime
                    long startTs;
                    var lastCandlestick = await _dbContext.MarketCandlesticks
                        .AsNoTracking()
                        .Where(c => c.Ticker == market.TickerId)
                        .OrderByDescending(c => c.EndPeriodTs)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (lastCandlestick != null)
                    {
                        // Continue from last synced candlestick
                        startTs = lastCandlestick.EndPeriodTs;
                        await _syncLogService.LogSyncEventAsync($"CandlesticksSync_FromLastSync_{market.TickerId}", 0, cancellationToken, LogType.DEBUG);
                    }
                    else
                    {
                        // Start from market creation time
                        startTs = new DateTimeOffset(snapshot.CreatedTime, TimeSpan.Zero).ToUnixTimeSeconds();
                        await _syncLogService.LogSyncEventAsync($"CandlesticksSync_FromCreation_{market.TickerId}", 0, cancellationToken, LogType.DEBUG);
                    }

                    // Skip if start time is in the future or same as end time
                    if (startTs >= endTs)
                    {
                        await _syncLogService.LogSyncEventAsync($"CandlesticksSync_UpToDate_{market.TickerId}", 0, cancellationToken, LogType.DEBUG);
                        continue;
                    }

                    await _syncLogService.LogSyncEventAsync($"CandlesticksSync_Fetching_{market.TickerId}", 0, cancellationToken, LogType.DEBUG);

                    var response = await _kalshiClient.Markets.GetMarketCandlesticksAsync(
                        seriesTicker,
                        market.TickerId,
                        startTs,
                        endTs,
                        60, // 1-hour period interval
                        cancellationToken: cancellationToken);

                    if (response?.Candlesticks == null || response.Candlesticks.Count == 0)
                    {
                        await _syncLogService.LogSyncEventAsync($"CandlesticksSync_NoData_{market.TickerId}", 0, cancellationToken, LogType.DEBUG);
                        continue;
                    }

                    foreach (var candle in response.Candlesticks)
                    {
                        var candlestickData = new MarketCandlestickData
                        {
                            // Id is auto-generated by ClickHouse via generateSerialID
                            Ticker = response.Ticker,
                            SeriesTicker = seriesTicker,
                            PeriodInterval = 60,
                            EndPeriodTs = candle.EndPeriodTs,
                            EndPeriodTime = DateTimeOffset.FromUnixTimeSeconds(candle.EndPeriodTs).UtcDateTime,
                            // Yes Bid
                            YesBidOpen = candle.YesBid.Open,
                            YesBidLow = candle.YesBid.Low,
                            YesBidHigh = candle.YesBid.High,
                            YesBidClose = candle.YesBid.Close,
                            YesBidOpenDollars = candle.YesBid.OpenDollars,
                            YesBidLowDollars = candle.YesBid.LowDollars,
                            YesBidHighDollars = candle.YesBid.HighDollars,
                            YesBidCloseDollars = candle.YesBid.CloseDollars,
                            // Yes Ask
                            YesAskOpen = candle.YesAsk.Open,
                            YesAskLow = candle.YesAsk.Low,
                            YesAskHigh = candle.YesAsk.High,
                            YesAskClose = candle.YesAsk.Close,
                            YesAskOpenDollars = candle.YesAsk.OpenDollars,
                            YesAskLowDollars = candle.YesAsk.LowDollars,
                            YesAskHighDollars = candle.YesAsk.HighDollars,
                            YesAskCloseDollars = candle.YesAsk.CloseDollars,
                            // Price (nullable)
                            PriceOpen = candle.Price?.Open,
                            PriceLow = candle.Price?.Low,
                            PriceHigh = candle.Price?.High,
                            PriceClose = candle.Price?.Close,
                            PriceMean = candle.Price?.Mean,
                            PricePrevious = candle.Price?.Previous,
                            PriceOpenDollars = candle.Price?.OpenDollars,
                            PriceLowDollars = candle.Price?.LowDollars,
                            PriceHighDollars = candle.Price?.HighDollars,
                            PriceCloseDollars = candle.Price?.CloseDollars,
                            PriceMeanDollars = candle.Price?.MeanDollars,
                            PricePreviousDollars = candle.Price?.PreviousDollars,
                            // Volume and interest
                            Volume = candle.Volume,
                            OpenInterest = candle.OpenInterest,
                            FetchedAt = now
                        };
                        candlesticksToInsert.Add(candlestickData);
                    }
                }
                catch (Exception ex)
                {
                    await _syncLogService.LogSyncEventAsync($"CandlesticksSync_FetchError_{market.TickerId}", 0, cancellationToken, LogType.ERROR);
                }
            }

            // Save all candlesticks
            if (candlesticksToInsert.Count > 0)
            {
                await _dbContext.MarketCandlesticks.AddRangeAsync(candlesticksToInsert, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await _syncLogService.LogSyncEventAsync("CandlesticksSync_Completed", candlesticksToInsert.Count, cancellationToken, LogType.Info);
        }
        catch (Exception ex)
        {
            await _syncLogService.LogSyncEventAsync("CandlesticksSync_Error", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }
}
