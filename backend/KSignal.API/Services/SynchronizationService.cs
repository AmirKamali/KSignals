using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Messaging;
using KSignal.API.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    public async Task EnqueueMarketSyncAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Queueing market synchronization (cursor={Cursor})", cursor ?? "<start>");
            await _publishEndpoint.Publish(new SynchronizeMarketData(cursor), cancellationToken);
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
        var request = BuildRequest(command.Cursor);
        var response = await _kalshiClient.Markets.AsynchronousClient.GetAsync<GetMarketsResponse>(
            "/markets",
            request,
            _kalshiClient.Markets.Configuration,
            cancellationToken);

        var payload = response?.Data ?? new GetMarketsResponse();
        var fetchedAt = DateTime.UtcNow;
        _logger.LogInformation("Fetched {Count} markets from Kalshi.API (cursor={Cursor})", payload.Markets.Count, command.Cursor ?? "<start>");

        var mapped = payload.Markets
            .Where(m => m != null)
            .Select(m =>
            {
                var seriesKey = string.IsNullOrWhiteSpace(m.EventTicker) ? m.Ticker : m.EventTicker;
                return KalshiService.MapMarket(seriesKey ?? m.Ticker, m, fetchedAt);
            })
            .ToList();

        await InsertMarketSnapshotsAsync(mapped, cancellationToken);

        if (!string.IsNullOrWhiteSpace(payload.Cursor))
        {
            _logger.LogInformation("Queueing next market sync page (cursor={Cursor})", payload.Cursor);
            await _publishEndpoint.Publish(new SynchronizeMarketData(payload.Cursor), cancellationToken);
        }
    }

    private static RequestOptions BuildRequest(string? cursor)
    {
        var requestOptions = new RequestOptions
        {
            Operation = "MarketApi.GetMarkets"
        };

        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "limit", 250));
        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "status", "open"));
        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "with_nested_markets", true));

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
                    // New record - insert it
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

        // Always insert new snapshots (no updates needed for snapshot table)
        await _dbContext.MarketSnapshots.AddRangeAsync(markets, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Inserted {Count} market snapshots", markets.Count);
    }
}
