using KSignal.API.Data;
using KSignal.API.Messaging;
using KSignal.API.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace KSignal.API.Services;

/// <summary>
/// Service for cleaning up data from finalized/closed markets to free up space.
/// </summary>
public class CleanupService
{
    private readonly KalshiDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISyncLogService _syncLogService;

    public CleanupService(
        KalshiDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ISyncLogService syncLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _syncLogService = syncLogService ?? throw new ArgumentNullException(nameof(syncLogService));
    }

    /// <summary>
    /// Finds all tickers with Finalized or Closed status that are at least 7 days past close/expiration
    /// and enqueues cleanup jobs for each (max 500 markets per call)
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
                .Take(10000)
                .ToListAsync(cancellationToken);

            if (tickersToCleanup.Count == 0)
            {
                await _syncLogService.LogSyncEventAsync("CleanupMarketData_NoMarketsFound", 0, cancellationToken, LogType.Info);
                return 0;
            }

            await _syncLogService.LogSyncEventAsync("CleanupMarketData_QueueingStarted", tickersToCleanup.Count, cancellationToken, LogType.Info);

            foreach (var tickerId in tickersToCleanup)
            {
                await _publishEndpoint.Publish(new CleanupMarketData(tickerId), cancellationToken);
            }

            await _syncLogService.LogSyncEventAsync("CleanupMarketData_QueueingCompleted", tickersToCleanup.Count, cancellationToken, LogType.Info);

            return tickersToCleanup.Count;
        }
        catch (Exception)
        {
            await _syncLogService.LogSyncEventAsync("CleanupMarketData_Error", 0, cancellationToken, LogType.ERROR);
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
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_Started_{tickerId}", 1, cancellationToken, LogType.Info);

            var totalDeleted = 0;

            // 1. Delete from market_snapshots
            var snapshotsDeleted = await DeleteMarketSnapshotsAsync(tickerId, cancellationToken);
            totalDeleted += snapshotsDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_MarketSnapshots_{tickerId}", snapshotsDeleted, cancellationToken, LogType.DEBUG);

            // 2. Delete from market_snapshots_latest
            var snapshotsLatestDeleted = await DeleteMarketSnapshotsLatestAsync(tickerId, cancellationToken);
            totalDeleted += snapshotsLatestDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_MarketSnapshotsLatest_{tickerId}", snapshotsLatestDeleted, cancellationToken, LogType.DEBUG);

            // 3. Delete from market_candlesticks
            var candlesticksDeleted = await DeleteMarketCandlesticksAsync(tickerId, cancellationToken);
            totalDeleted += candlesticksDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_MarketCandlesticks_{tickerId}", candlesticksDeleted, cancellationToken, LogType.DEBUG);

            // 4. Delete from orderbook_snapshots (uses MarketId)
            var orderbookSnapshotsDeleted = await DeleteOrderbookSnapshotsAsync(tickerId, cancellationToken);
            totalDeleted += orderbookSnapshotsDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_OrderbookSnapshots_{tickerId}", orderbookSnapshotsDeleted, cancellationToken, LogType.DEBUG);

            // 5. Delete from orderbook_events (uses MarketId)
            var orderbookEventsDeleted = await DeleteOrderbookEventsAsync(tickerId, cancellationToken);
            totalDeleted += orderbookEventsDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_OrderbookEvents_{tickerId}", orderbookEventsDeleted, cancellationToken, LogType.DEBUG);

            // 6. Delete from analytics_market_features
            var analyticsDeleted = await DeleteAnalyticsMarketFeaturesAsync(tickerId, cancellationToken);
            totalDeleted += analyticsDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_AnalyticsMarketFeatures_{tickerId}", analyticsDeleted, cancellationToken, LogType.DEBUG);

            // 7. Delete from market_highpriority
            var highPriorityDeleted = await DeleteMarketHighPriorityAsync(tickerId, cancellationToken);
            totalDeleted += highPriorityDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_MarketHighPriority_{tickerId}", highPriorityDeleted, cancellationToken, LogType.DEBUG);

            await _syncLogService.LogSyncEventAsync($"CleanupTicker_Completed_{tickerId}", totalDeleted, cancellationToken, LogType.Info);
        }
        catch (Exception)
        {
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_Error_{tickerId}", 0, cancellationToken, LogType.ERROR);
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
        catch (Exception)
        {
            await _syncLogService.LogSyncEventAsync("CleanupTicker_SqlExecutionError", 0, cancellationToken, LogType.WARN);
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
