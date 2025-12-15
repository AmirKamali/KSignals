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
                .Take(10)
                .ToListAsync(cancellationToken);

            if (tickersToCleanup.Count == 0)
            {
                await _syncLogService.LogSyncEventAsync("CleanupMarketData_NoMarketsFound", 0, cancellationToken, LogType.Info);
                return 0;
            }

            await _syncLogService.LogSyncEventAsync("CleanupMarketData_QueueingStarted", tickersToCleanup.Count, cancellationToken, LogType.Info);



            await _publishEndpoint.Publish(new CleanupMarketData(tickersToCleanup.ToArray()), cancellationToken);


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
    /// Cleans up all data for specific tickers from all related tables
    /// </summary>
    public async Task CleanupMarketDataAsync(string[] tickerIds, CancellationToken cancellationToken = default)
    {
        if (tickerIds == null || tickerIds.Length == 0)
        {
            return;
        }

        try
        {
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_Started_Batch_{tickerIds.Length}", tickerIds.Length, cancellationToken, LogType.Info);

            var totalDeleted = 0;

            // 1. Delete from market_snapshots
            var snapshotsDeleted = await DeleteMarketSnapshotsAsync(tickerIds, cancellationToken);
            totalDeleted += snapshotsDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_MarketSnapshots_Batch_{tickerIds.Length}", snapshotsDeleted, cancellationToken, LogType.DEBUG);

            // 2. Delete from market_snapshots_latest
            var snapshotsLatestDeleted = await DeleteMarketSnapshotsLatestAsync(tickerIds, cancellationToken);
            totalDeleted += snapshotsLatestDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_MarketSnapshotsLatest_Batch_{tickerIds.Length}", snapshotsLatestDeleted, cancellationToken, LogType.DEBUG);

            // 3. Delete from market_candlesticks
            var candlesticksDeleted = await DeleteMarketCandlesticksAsync(tickerIds, cancellationToken);
            totalDeleted += candlesticksDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_MarketCandlesticks_Batch_{tickerIds.Length}", candlesticksDeleted, cancellationToken, LogType.DEBUG);

            // 4. Delete from orderbook_snapshots (uses MarketId)
            var orderbookSnapshotsDeleted = await DeleteOrderbookSnapshotsAsync(tickerIds, cancellationToken);
            totalDeleted += orderbookSnapshotsDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_OrderbookSnapshots_Batch_{tickerIds.Length}", orderbookSnapshotsDeleted, cancellationToken, LogType.DEBUG);

            // 5. Delete from orderbook_events (uses MarketId)
            var orderbookEventsDeleted = await DeleteOrderbookEventsAsync(tickerIds, cancellationToken);
            totalDeleted += orderbookEventsDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_OrderbookEvents_Batch_{tickerIds.Length}", orderbookEventsDeleted, cancellationToken, LogType.DEBUG);

            // 6. Delete from analytics_market_features
            var analyticsDeleted = await DeleteAnalyticsMarketFeaturesAsync(tickerIds, cancellationToken);
            totalDeleted += analyticsDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_AnalyticsMarketFeatures_Batch_{tickerIds.Length}", analyticsDeleted, cancellationToken, LogType.DEBUG);

            // 7. Delete from market_highpriority
            var highPriorityDeleted = await DeleteMarketHighPriorityAsync(tickerIds, cancellationToken);
            totalDeleted += highPriorityDeleted;
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_MarketHighPriority_Batch_{tickerIds.Length}", highPriorityDeleted, cancellationToken, LogType.DEBUG);

            await _syncLogService.LogSyncEventAsync($"CleanupTicker_Completed_Batch_{tickerIds.Length}", totalDeleted, cancellationToken, LogType.Info);
        }
        catch (Exception)
        {
            await _syncLogService.LogSyncEventAsync($"CleanupTicker_Error_Batch_{tickerIds.Length}", 0, cancellationToken, LogType.ERROR);
            throw;
        }
    }

    private async Task<int> DeleteMarketSnapshotsAsync(string[] tickerIds, CancellationToken cancellationToken)
    {
        if (tickerIds == null || tickerIds.Length == 0)
        {
            return 0;
        }

        // ClickHouse requires ALTER TABLE ... DELETE for MergeTree tables
        var tickerList = string.Join(",", tickerIds.Select(t => $"'{EscapeSqlString(t)}'"));
        var sql = $"ALTER TABLE kalshi_signals.market_snapshots DELETE WHERE Ticker IN ({tickerList})";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteMarketSnapshotsLatestAsync(string[] tickerIds, CancellationToken cancellationToken)
    {
        if (tickerIds == null || tickerIds.Length == 0)
        {
            return 0;
        }

        // ClickHouse requires ALTER TABLE ... DELETE for MergeTree tables
        var tickerList = string.Join(",", tickerIds.Select(t => $"'{EscapeSqlString(t)}'"));
        var sql = $"ALTER TABLE kalshi_signals.market_snapshots_latest DELETE WHERE Ticker IN ({tickerList})";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteMarketCandlesticksAsync(string[] tickerIds, CancellationToken cancellationToken)
    {
        if (tickerIds == null || tickerIds.Length == 0)
        {
            return 0;
        }

        var tickerList = string.Join(",", tickerIds.Select(t => $"'{EscapeSqlString(t)}'"));
        var sql = $"ALTER TABLE kalshi_signals.market_candlesticks DELETE WHERE Ticker IN ({tickerList})";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteOrderbookSnapshotsAsync(string[] tickerIds, CancellationToken cancellationToken)
    {
        if (tickerIds == null || tickerIds.Length == 0)
        {
            return 0;
        }

        var tickerList = string.Join(",", tickerIds.Select(t => $"'{EscapeSqlString(t)}'"));
        var sql = $"ALTER TABLE kalshi_signals.orderbook_snapshots DELETE WHERE MarketId IN ({tickerList})";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteOrderbookEventsAsync(string[] tickerIds, CancellationToken cancellationToken)
    {
        if (tickerIds == null || tickerIds.Length == 0)
        {
            return 0;
        }

        var tickerList = string.Join(",", tickerIds.Select(t => $"'{EscapeSqlString(t)}'"));
        var sql = $"ALTER TABLE kalshi_signals.orderbook_events DELETE WHERE MarketId IN ({tickerList})";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteAnalyticsMarketFeaturesAsync(string[] tickerIds, CancellationToken cancellationToken)
    {
        if (tickerIds == null || tickerIds.Length == 0)
        {
            return 0;
        }

        var tickerList = string.Join(",", tickerIds.Select(t => $"'{EscapeSqlString(t)}'"));
        var sql = $"ALTER TABLE kalshi_signals.analytics_market_features DELETE WHERE Ticker IN ({tickerList})";
        return await ExecuteRawSqlAsync(sql, cancellationToken);
    }

    private async Task<int> DeleteMarketHighPriorityAsync(string[] tickerIds, CancellationToken cancellationToken)
    {
        if (tickerIds == null || tickerIds.Length == 0)
        {
            return 0;
        }

        var tickerList = string.Join(",", tickerIds.Select(t => $"'{EscapeSqlString(t)}'"));
        var sql = $"ALTER TABLE kalshi_signals.market_highpriority DELETE WHERE TickerId IN ({tickerList})";
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
