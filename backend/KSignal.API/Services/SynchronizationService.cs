using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Messaging;
using KSignal.API.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KSignal.API.Services;

public class SynchronizationService
{
    private readonly KalshiClient _kalshiClient;
    private readonly KalshiDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<SynchronizationService> _logger;

    public SynchronizationService(
        KalshiClient kalshiClient,
        KalshiDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<SynchronizationService> logger)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnqueueMarketSyncAsync(string? cursor, string? marketTickerId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(marketTickerId))
            {
                _logger.LogInformation("Queueing single market synchronization for ticker={TickerId}", marketTickerId);
                await _publishEndpoint.Publish(new SynchronizeMarketData(null, cursor, marketTickerId), cancellationToken);
                return;
            }
            
            // Load all seriesIds from market_series and enqueue each
            var seriesIds = await _dbContext.MarketSeries
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .Select(s => s.Ticker)
                .ToListAsync(cancellationToken);

            if (seriesIds.Count == 0)
            {
                _logger.LogWarning("No series found in market_series table. Please sync market series first.");
                return;
            }

            _logger.LogInformation("Queueing market synchronization for {Count} series", seriesIds.Count);
            
            foreach (var seriesId in seriesIds)
            {
                await _publishEndpoint.Publish(new SynchronizeMarketData(seriesId, null), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Re-throw other exceptions as-is
            _logger.LogError(ex, "Unexpected error while trying to enqueue synchronization. Exception type: {ExceptionType}", ex.GetType().Name);
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

        // SeriesId is required for bulk sync
        if (string.IsNullOrWhiteSpace(command.SeriesId))
        {
            _logger.LogWarning("SeriesId is required for market synchronization");
            return;
        }

        // Sync markets for a specific series
        var request = BuildRequest(command.SeriesId, command.Cursor);
        var response = await _kalshiClient.Markets.AsynchronousClient.GetAsync<GetMarketsResponse>(
            "/markets",
            request,
            _kalshiClient.Markets.Configuration,
            cancellationToken);

        var payload = response?.Data ?? new GetMarketsResponse();
        var fetchedAt = DateTime.UtcNow;
        _logger.LogInformation("Fetched {Count} markets from Kalshi.API for series={SeriesId} (cursor={Cursor})", 
            payload.Markets.Count, command.SeriesId, command.Cursor ?? "<start>");

        var mapped = payload.Markets
            .Where(m => m != null)
            .Select(m =>
            {
                var seriesKey = string.IsNullOrWhiteSpace(m.EventTicker) ? m.Ticker : m.EventTicker;
                return KalshiService.MapMarket(seriesKey ?? m.Ticker, command.SeriesId, m, fetchedAt);
            })
            .ToList();

        await InsertMarketSnapshotsAsync(mapped, cancellationToken);

        if (!string.IsNullOrWhiteSpace(payload.Cursor))
        {
            _logger.LogInformation("Queueing next market sync page for series={SeriesId} (cursor={Cursor})", command.SeriesId, payload.Cursor);
            await _publishEndpoint.Publish(new SynchronizeMarketData(command.SeriesId, payload.Cursor), cancellationToken);
        }
    }

    private async Task SynchronizeSingleMarketAsync(string marketTickerId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching single market from Kalshi API: {TickerId}", marketTickerId);

            var response = await _kalshiClient.Markets.GetMarketAsync(marketTickerId, cancellationToken: cancellationToken);
            var market = response?.Market;

            if (market == null)
            {
                _logger.LogWarning("Market {TickerId} was not returned from Kalshi API", marketTickerId);
                return;
            }

            var fetchedAt = DateTime.UtcNow;
            var seriesKey = string.IsNullOrWhiteSpace(market.EventTicker) ? market.Ticker : market.EventTicker;
            
            // Look up the seriesId from market_events table using EventTicker
            var seriesId = string.Empty;
            if (!string.IsNullOrWhiteSpace(market.EventTicker))
            {
                var marketEvent = await _dbContext.MarketEvents
                    .AsNoTracking()
                    .Where(e => e.EventTicker == market.EventTicker)
                    .FirstOrDefaultAsync(cancellationToken);
                seriesId = marketEvent?.SeriesTicker ?? string.Empty;
            }
            
            var mapped = KalshiService.MapMarket(seriesKey ?? marketTickerId, seriesId, market, fetchedAt);

            await InsertMarketSnapshotsAsync(new[] { mapped }, cancellationToken);
            
            _logger.LogInformation("Successfully synchronized market {TickerId}", marketTickerId);
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error fetching market {TickerId}", marketTickerId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing market {TickerId}", marketTickerId);
            throw;
        }
    }

    private static RequestOptions BuildRequest(string seriesId, string? cursor)
    {
        var requestOptions = new RequestOptions
        {
            Operation = "MarketApi.GetMarkets"
        };

        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "limit", 250));
        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "status", "open"));
        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "with_nested_markets", true));
        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "series_ticker", seriesId));

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
            _logger.LogInformation("Queueing tags and categories synchronization");
            await _publishEndpoint.Publish(new SynchronizeTagsCategories(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while trying to enqueue tags/categories synchronization. Exception type: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    public async Task SynchronizeTagsCategoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching tags and categories from Kalshi API");
            
            var response = await _kalshiClient.Search.GetTagsForSeriesCategoriesAsync(cancellationToken: cancellationToken);
            
            if (response?.TagsByCategories == null || response.TagsByCategories.Count == 0)
            {
                _logger.LogWarning("No tags/categories data received from Kalshi API");
                return;
            }

            _logger.LogInformation("Received {Count} categories with tags from Kalshi API", response.TagsByCategories.Count);

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
                _logger.LogWarning("No valid tags/categories to save");
                return;
            }

            // Load all existing records
            var existingRecords = await _dbContext.TagsCategories
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} existing tags/categories records", existingRecords.Count);

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
            
            _logger.LogInformation(
                "Successfully synchronized tags/categories: {Inserted} inserted, {Updated} updated, {Restored} restored, {Deleted} marked as deleted",
                insertedCount, updatedCount, restoredCount, deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing tags and categories");
            throw;
        }
    }

    private async Task InsertMarketSnapshotsAsync(IReadOnlyCollection<MarketSnapshot> markets, CancellationToken cancellationToken)
    {
        if (markets.Count == 0)
        {
            _logger.LogInformation("No market snapshots to insert for this page");
            return;
        }

        // Generate IDs manually (ClickHouse generateSerialID requires ZooKeeper which may not be configured)
        var maxId = await _dbContext.MarketSnapshots.MaxAsync(s => (ulong?)s.MarketSnapshotID, cancellationToken) ?? 0;
        var nextId = maxId + 1;
        
        foreach (var market in markets)
        {
            market.MarketSnapshotID = nextId++;
        }

        // Always insert new snapshots (no updates needed for snapshot table)
        await _dbContext.MarketSnapshots.AddRangeAsync(markets, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Inserted {Count} market snapshots", markets.Count);
    }

    public async Task EnqueueSeriesSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Queueing series synchronization");
            await _publishEndpoint.Publish(new SynchronizeSeries(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while trying to enqueue series synchronization. Exception type: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    public async Task SynchronizeSeriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching series list from Kalshi API");
            
            var response = await _kalshiClient.Markets.GetSeriesListAsync(
                includeProductMetadata: true,
                cancellationToken: cancellationToken);
            
            if (response?.Series == null || response.Series.Count == 0)
            {
                _logger.LogWarning("No series data received from Kalshi API");
                return;
            }

            _logger.LogInformation("Received {Count} series from Kalshi API", response.Series.Count);

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
                _logger.LogWarning("No valid series to save");
                return;
            }

            // Load all existing records
            var existingRecords = await _dbContext.MarketSeries
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} existing series records", existingRecords.Count);

            var existingDict = existingRecords
                .ToDictionary(e => e.Ticker, e => e);

            var updatedCount = 0;
            var insertedCount = 0;
            var deletedCount = 0;
            var restoredCount = 0;

            // Process incoming records: upsert logic
            foreach (var incoming in incomingRecords)
            {
                if (existingDict.TryGetValue(incoming.Ticker, out var existing))
                {
                    // Record exists - update it
                    if (existing.IsDeleted)
                    {
                        restoredCount++;
                    }
                    
                    // Update all fields
                    existing.Frequency = incoming.Frequency;
                    existing.Title = incoming.Title;
                    existing.Category = incoming.Category;
                    existing.Tags = incoming.Tags;
                    existing.SettlementSources = incoming.SettlementSources;
                    existing.ContractUrl = incoming.ContractUrl;
                    existing.ContractTermsUrl = incoming.ContractTermsUrl;
                    existing.ProductMetadata = incoming.ProductMetadata;
                    existing.FeeType = incoming.FeeType;
                    existing.FeeMultiplier = incoming.FeeMultiplier;
                    existing.AdditionalProhibitions = incoming.AdditionalProhibitions;
                    existing.LastUpdate = now;
                    existing.IsDeleted = false;
                    
                    updatedCount++;
                }
                else
                {
                    // New record - insert it (Ticker is the primary key)
                    await _dbContext.MarketSeries.AddAsync(incoming, cancellationToken);
                    insertedCount++;
                }
            }

            // Mark records as deleted if they're not in incoming data
            foreach (var existing in existingRecords)
            {
                if (!incomingTickers.Contains(existing.Ticker) && !existing.IsDeleted)
                {
                    existing.IsDeleted = true;
                    existing.LastUpdate = now;
                    deletedCount++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation(
                "Successfully synchronized series: {Inserted} inserted, {Updated} updated, {Restored} restored, {Deleted} marked as deleted",
                insertedCount, updatedCount, restoredCount, deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing series");
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
            _logger.LogInformation("Queueing events synchronization (cursor={Cursor})", cursor ?? "<start>");
            await _publishEndpoint.Publish(new SynchronizeEvents(cursor), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while trying to enqueue events synchronization. Exception type: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    public async Task SynchronizeEventsAsync(SynchronizeEvents command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching events from Kalshi API (cursor={Cursor})", command.Cursor ?? "<start>");
            
            var response = await _kalshiClient.Events.GetEventsAsync(
                limit: 200,
                cursor: command.Cursor,
                withNestedMarkets: false,
                cancellationToken: cancellationToken);
            
            if (response?.Events == null || response.Events.Count == 0)
            {
                _logger.LogInformation("No more events to sync");
                return;
            }

            _logger.LogInformation("Received {Count} events from Kalshi API", response.Events.Count);

            var now = DateTime.UtcNow;
            var eventsToInsert = new List<MarketEvent>();
            var existingTickers = new HashSet<string>();

            // Get existing event tickers for this batch
            var incomingTickers = response.Events.Select(e => e.EventTicker).ToList();
            var existingEvents = await _dbContext.MarketEvents
                .Where(e => incomingTickers.Contains(e.EventTicker))
                .ToDictionaryAsync(e => e.EventTicker, e => e, cancellationToken);

            foreach (var eventData in response.Events)
            {
                if (string.IsNullOrWhiteSpace(eventData.EventTicker))
                    continue;

                if (existingEvents.TryGetValue(eventData.EventTicker, out var existing))
                {
                    // Update existing record
                    existing.SeriesTicker = eventData.SeriesTicker;
                    existing.SubTitle = eventData.SubTitle;
                    existing.Title = eventData.Title;
                    existing.CollateralReturnType = eventData.CollateralReturnType;
                    existing.MutuallyExclusive = eventData.MutuallyExclusive;
                    existing.Category = eventData.Category;
                    existing.StrikeDate = eventData.StrikeDate;
                    existing.StrikePeriod = eventData.StrikePeriod;
                    existing.AvailableOnBrokers = eventData.AvailableOnBrokers;
                    existing.ProductMetadata = eventData.ProductMetadata != null 
                        ? JsonConvert.SerializeObject(eventData.ProductMetadata) 
                        : null;
                    existing.LastUpdate = now;
                    existing.IsDeleted = false;
                }
                else
                {
                    // Insert new record
                    eventsToInsert.Add(MapEventDataToMarketEvent(eventData, now));
                }
            }

            if (eventsToInsert.Count > 0)
            {
                await _dbContext.MarketEvents.AddRangeAsync(eventsToInsert, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Synchronized {Updated} updated, {Inserted} inserted events", 
                existingEvents.Count, eventsToInsert.Count);

            // Queue next page if there's more data
            if (!string.IsNullOrWhiteSpace(response.Cursor))
            {
                _logger.LogInformation("Queueing next events sync page (cursor={Cursor})", response.Cursor);
                await _publishEndpoint.Publish(new SynchronizeEvents(response.Cursor), cancellationToken);
            }
            else
            {
                _logger.LogInformation("Events synchronization complete - no more pages");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing events");
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
            _logger.LogInformation("Queueing orderbook synchronization");
            await _publishEndpoint.Publish(new SynchronizeOrderbook(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while trying to enqueue orderbook synchronization. Exception type: {ExceptionType}", ex.GetType().Name);
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
                _logger.LogWarning("No high-priority markets configured for orderbook sync");
                return;
            }

            _logger.LogInformation("Syncing orderbooks for {Count} high-priority markets", highPriorityMarkets.Count);

            var now = DateTime.UtcNow;
            var snapshotsToInsert = new List<OrderbookSnapshot>();
            var eventsToInsert = new List<OrderbookEvent>();

            // Get max ID for generating new IDs
            var maxSnapshotId = await _dbContext.OrderbookSnapshots.MaxAsync(s => (long?)s.Id, cancellationToken) ?? 0;
            var maxEventId = await _dbContext.OrderbookEvents.MaxAsync(e => (long?)e.Id, cancellationToken) ?? 0;
            var nextSnapshotId = maxSnapshotId + 1;
            var nextEventId = maxEventId + 1;

            foreach (var market in highPriorityMarkets)
            {
                try
                {
                    _logger.LogDebug("Fetching orderbook for market {TickerId}", market.TickerId);
                    
                    var response = await _kalshiClient.Markets.GetMarketOrderbookAsync(
                        market.TickerId,
                        depth: 0, // Get all levels
                        cancellationToken: cancellationToken);

                    if (response?.Orderbook == null || (response.Orderbook.Yes == null && response.Orderbook.No == null))
                    {
                        _logger.LogWarning("No orderbook data for market {TickerId}", market.TickerId);
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

                    // Create snapshot
                    var snapshot = new OrderbookSnapshot
                    {
                        Id = nextSnapshotId++,
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
                        now, 
                        ref nextEventId);
                    eventsToInsert.AddRange(events);
                    
                    // Note: We don't update LastUpdate on market_highpriority here because
                    // ClickHouse ReplacingMergeTree uses LastUpdate as the version column
                    // and doesn't allow direct updates. The sync time can be tracked via
                    // orderbook_snapshots.CapturedAt instead.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching orderbook for market {TickerId}", market.TickerId);
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

            _logger.LogInformation("Orderbook sync complete: {Snapshots} snapshots, {Events} events created",
                snapshotsToInsert.Count, eventsToInsert.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing orderbooks");
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
        DateTime eventTime,
        ref long nextEventId)
    {
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
                    Id = nextEventId++,
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
                    Id = nextEventId++,
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
                    Id = nextEventId++,
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
                    Id = nextEventId++,
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
                    Id = nextEventId++,
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
                    Id = nextEventId++,
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
            _logger.LogInformation("Queueing candlesticks synchronization");
            await _publishEndpoint.Publish(new SynchronizeCandlesticks(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while trying to enqueue candlesticks synchronization. Exception type: {ExceptionType}", ex.GetType().Name);
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
                _logger.LogWarning("No high-priority markets configured for candlestick sync");
                return;
            }

            _logger.LogInformation("Syncing candlesticks for {Count} high-priority markets", highPriorityMarkets.Count);

            var now = DateTime.UtcNow;
            var candlesticksToInsert = new List<MarketCandlestickData>();

            // Get max ID for generating new IDs
            var maxId = await _dbContext.MarketCandlesticks.MaxAsync(c => (long?)c.Id, cancellationToken) ?? 0;
            var nextId = maxId + 1;

            // Calculate time range: last 24 hours
            var endTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var startTs = endTs - (24 * 60 * 60); // 24 hours ago

            foreach (var market in highPriorityMarkets)
            {
                try
                {
                    // Find the market snapshot to get SeriesId
                    var snapshot = await _dbContext.MarketSnapshots
                        .AsNoTracking()
                        .Where(s => s.Ticker == market.TickerId && s.Status == "Active")
                        .OrderByDescending(s => s.GenerateDate)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (snapshot == null)
                    {
                        _logger.LogWarning("No active market snapshot found for ticker {TickerId}", market.TickerId);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(snapshot.SeriesId))
                    {
                        _logger.LogWarning("Market snapshot for ticker {TickerId} has no SeriesId", market.TickerId);
                        continue;
                    }

                    _logger.LogDebug("Fetching candlesticks for market {TickerId} (series: {SeriesId})", market.TickerId, snapshot.SeriesId);

                    var response = await _kalshiClient.Markets.GetMarketCandlesticksAsync(
                        snapshot.SeriesId,
                        market.TickerId,
                        startTs,
                        endTs,
                        60, // 1-hour period interval
                        cancellationToken: cancellationToken);

                    if (response?.Candlesticks == null || response.Candlesticks.Count == 0)
                    {
                        _logger.LogDebug("No candlestick data for market {TickerId}", market.TickerId);
                        continue;
                    }

                    _logger.LogInformation("Fetched {Count} candlesticks for market {TickerId}", response.Candlesticks.Count, market.TickerId);

                    foreach (var candle in response.Candlesticks)
                    {
                        var candlestickData = new MarketCandlestickData
                        {
                            Id = nextId++,
                            Ticker = response.Ticker,
                            SeriesTicker = snapshot.SeriesId,
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
                    _logger.LogError(ex, "Error fetching candlesticks for market {TickerId}", market.TickerId);
                }
            }

            // Save all candlesticks
            if (candlesticksToInsert.Count > 0)
            {
                await _dbContext.MarketCandlesticks.AddRangeAsync(candlesticksToInsert, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Candlestick sync complete: {Count} candlesticks created", candlesticksToInsert.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing candlesticks");
            throw;
        }
    }
}
