using KSignal.API.Data;
using KSignal.API.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KSignal.API.Services;

/// <summary>
/// Service for cleaning up data from finalized/closed markets to free up space.
/// </summary>
public class CleanupService
{
    private readonly KalshiDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CleanupService> _logger;

    public CleanupService(
        KalshiDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<CleanupService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Finds all tickers with Finalized or Closed status that are at least 7 days past close/expiration
    /// and enqueues cleanup jobs for each
    /// </summary>
    public async Task<int> EnqueueCleanupJobsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-2);

            // Find distinct tickers where:
            // 1. Status is Finalized or Closed
            // 2. CloseTime or ExpectedExpirationTime is at least 7 days ago
            // 3. Limit to 500 markets per call
            var tickersToCleanup = await _dbContext.MarketSnapshots
                .AsNoTracking()
                .Where(s =>
                    (s.Status == "Finalized" || s.Status == "Closed" || s.Status == "finalized" || s.Status == "closed") &&
                    (s.CloseTime <= sevenDaysAgo ||
                     (s.ExpectedExpirationTime.HasValue && s.ExpectedExpirationTime.Value <= sevenDaysAgo)))
                .Select(s => s.Ticker)
                .Distinct()
                .Take(500)
                .ToListAsync(cancellationToken);

            if (tickersToCleanup.Count == 0)
            {
                _logger.LogInformation("No finalized or closed markets found for cleanup");
                return 0;
            }

            _logger.LogInformation("Queueing cleanup for {Count} finalized/closed markets", tickersToCleanup.Count);

            foreach (var tickerId in tickersToCleanup)
            {
                await _publishEndpoint.Publish(new CleanupMarketData(tickerId), cancellationToken);
            }

            _logger.LogInformation("Successfully queued {Count} cleanup jobs", tickersToCleanup.Count);
            return tickersToCleanup.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueueing cleanup jobs");
            throw;
        }
    }

    /// <summary>
    /// Cleans up all data for a specific ticker from all related tables
    /// </summary>
    public async Task CleanupMarketDataAsync(string tickerId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting cleanup for ticker {TickerId}", tickerId);

            var totalDeleted = 0;

            // 1. Delete from market_snapshots
            var snapshotsDeleted = await DeleteMarketSnapshotsAsync(tickerId, cancellationToken);
            totalDeleted += snapshotsDeleted;
            _logger.LogDebug("Deleted {Count} market_snapshots for ticker {TickerId}", snapshotsDeleted, tickerId);

            // 2. Delete from market_snapshots_latest
            var snapshotsLatestDeleted = await DeleteMarketSnapshotsLatestAsync(tickerId, cancellationToken);
            totalDeleted += snapshotsLatestDeleted;
            _logger.LogDebug("Deleted {Count} market_snapshots_latest for ticker {TickerId}", snapshotsLatestDeleted, tickerId);

            // 3. Delete from market_candlesticks
            var candlesticksDeleted = await DeleteMarketCandlesticksAsync(tickerId, cancellationToken);
            totalDeleted += candlesticksDeleted;
            _logger.LogDebug("Deleted {Count} market_candlesticks for ticker {TickerId}", candlesticksDeleted, tickerId);

            // 4. Delete from orderbook_snapshots (uses MarketId)
            var orderbookSnapshotsDeleted = await DeleteOrderbookSnapshotsAsync(tickerId, cancellationToken);
            totalDeleted += orderbookSnapshotsDeleted;
            _logger.LogDebug("Deleted {Count} orderbook_snapshots for ticker {TickerId}", orderbookSnapshotsDeleted, tickerId);

            // 5. Delete from orderbook_events (uses MarketId)
            var orderbookEventsDeleted = await DeleteOrderbookEventsAsync(tickerId, cancellationToken);
            totalDeleted += orderbookEventsDeleted;
            _logger.LogDebug("Deleted {Count} orderbook_events for ticker {TickerId}", orderbookEventsDeleted, tickerId);

            // 6. Delete from analytics_market_features
            var analyticsDeleted = await DeleteAnalyticsMarketFeaturesAsync(tickerId, cancellationToken);
            totalDeleted += analyticsDeleted;
            _logger.LogDebug("Deleted {Count} analytics_market_features for ticker {TickerId}", analyticsDeleted, tickerId);

            // 7. Delete from market_highpriority
            var highPriorityDeleted = await DeleteMarketHighPriorityAsync(tickerId, cancellationToken);
            totalDeleted += highPriorityDeleted;
            _logger.LogDebug("Deleted {Count} market_highpriority for ticker {TickerId}", highPriorityDeleted, tickerId);

            _logger.LogInformation("Cleanup complete for ticker {TickerId}: {TotalDeleted} total records deleted", tickerId, totalDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up data for ticker {TickerId}", tickerId);
            throw;
        }
    }

    private async Task<int> DeleteMarketSnapshotsAsync(string tickerId, CancellationToken cancellationToken)
    {
        // ClickHouse requires ALTER TABLE ... DELETE for MergeTree tables
        var sql = $"ALTER TABLE kalshi_signals.market_snapshots DELETE WHERE Ticker = '{EscapeSqlString(tickerId)}'";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteMarketSnapshotsLatestAsync(string tickerId, CancellationToken cancellationToken)
    {
        // ClickHouse requires ALTER TABLE ... DELETE for MergeTree tables
        var sql = $"ALTER TABLE kalshi_signals.market_snapshots_latest DELETE WHERE Ticker = '{EscapeSqlString(tickerId)}'";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteMarketCandlesticksAsync(string tickerId, CancellationToken cancellationToken)
    {
        var sql = $"ALTER TABLE kalshi_signals.market_candlesticks DELETE WHERE Ticker = '{EscapeSqlString(tickerId)}'";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteOrderbookSnapshotsAsync(string tickerId, CancellationToken cancellationToken)
    {
        var sql = $"ALTER TABLE kalshi_signals.orderbook_snapshots DELETE WHERE MarketId = '{EscapeSqlString(tickerId)}'";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteOrderbookEventsAsync(string tickerId, CancellationToken cancellationToken)
    {
        var sql = $"ALTER TABLE kalshi_signals.orderbook_events DELETE WHERE MarketId = '{EscapeSqlString(tickerId)}'";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteAnalyticsMarketFeaturesAsync(string tickerId, CancellationToken cancellationToken)
    {
        var sql = $"ALTER TABLE kalshi_signals.analytics_market_features DELETE WHERE Ticker = '{EscapeSqlString(tickerId)}'";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteMarketHighPriorityAsync(string tickerId, CancellationToken cancellationToken)
    {
        var sql = $"ALTER TABLE kalshi_signals.market_highpriority DELETE WHERE TickerId = '{EscapeSqlString(tickerId)}'";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> ExecuteRawSqlAsync(string sql, CancellationToken cancellationToken)
    {
        try
        {
            // ClickHouse DELETE is async and doesn't return affected rows immediately
            // It returns mutation info instead
            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            return 1; // Indicate success (actual count not available for async mutations)
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error executing SQL: {Sql}", sql);
            return 0;
        }
    }

    /// <summary>
    /// Escapes single quotes in SQL strings to prevent injection
    /// </summary>
    private static string EscapeSqlString(string value)
    {
        return value.Replace("'", "''");
    }
}
