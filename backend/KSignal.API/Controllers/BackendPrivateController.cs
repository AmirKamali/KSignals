using Kalshi.Api.Client;
using KSignal.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

/// <summary>
/// Private API for data source refresh
/// </summary>
[ApiController]
[Route("api/private/data-source/")]
[Produces("application/json")]
public class BackendPrivateController : ControllerBase
{
    private readonly KalshiService _kalshiService;
    private readonly SynchronizationService _synchronizationService;
    private readonly CleanupService _cleanupService;
    private readonly RabbitMqManagementService _rabbitMqManagementService;
    private readonly IRedisCacheService _redisCacheService;
    private readonly ILogger<BackendPrivateController> _logger;

    private const string MarketSyncLockKey = "sync:market-snapshots:lock";
    private const string MarketSyncCounterKey = "sync:market-snapshots:pending";

    public BackendPrivateController(
        KalshiService kalshiService,
        SynchronizationService synchronizationService,
        CleanupService cleanupService,
        RabbitMqManagementService rabbitMqManagementService,
        IRedisCacheService redisCacheService,
        ILogger<BackendPrivateController> logger)
    {
        _kalshiService = kalshiService ?? throw new ArgumentNullException(nameof(kalshiService));
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
        _rabbitMqManagementService = rabbitMqManagementService ?? throw new ArgumentNullException(nameof(rabbitMqManagementService));
        _redisCacheService = redisCacheService ?? throw new ArgumentNullException(nameof(redisCacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Note: These endpoints are disabled because RefreshService is currently commented out
    // [HttpPost("refresh-series-categories-tags")]
    // [HttpGet("category-refresh-status")]
    // [HttpPost("refresh-market-data")]
    // [HttpGet("cache-market-status")]
    // [HttpPost("refresh-today-market")]

    [HttpPost("sync-market-snapshots")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SynchronizeMarketData(
        [FromQuery] long? min_created_ts = null,
        [FromQuery] long? max_created_ts = null,
        [FromQuery] string? status = null)
    {
        try
        {
            await _synchronizationService.EnqueueMarketSyncAsync(min_created_ts, max_created_ts, status, HttpContext.RequestAborted);

            return Accepted(new
            {
                started = true,
                min_created_ts = min_created_ts,
                max_created_ts = max_created_ts,
                status = status,
                message = "Market synchronization queued. Pagination will be handled automatically."
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already in progress"))
        {
            _logger.LogWarning(ex, "Market synchronization already in progress");
            return Conflict(new
            {
                error = "Synchronization already in progress",
                message = ex.Message,
                hint = "Use GET /api/private/data-source/sync-market-snapshots/status to check current status"
            });
        }
        catch (RabbitMqUnavailableException ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable while trying to enqueue synchronization");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "RabbitMQ unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue market synchronization");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to enqueue market synchronization", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current status of the market snapshots synchronization.
    /// Shows whether sync is running and how many jobs are pending.
    /// </summary>
    /// <returns>Status information about the current sync operation</returns>
    [HttpGet("sync-market-snapshots/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMarketSyncStatus()
    {
        try
        {
            var isLocked = await _redisCacheService.IsLockedAsync(MarketSyncLockKey);
            var pendingJobs = await _redisCacheService.GetCounterAsync(MarketSyncCounterKey);

            return Ok(new
            {
                is_running = isLocked,
                pending_jobs = pendingJobs,
                message = isLocked
                    ? $"Synchronization is in progress with {pendingJobs} pending job(s)"
                    : "No synchronization is currently running"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get market sync status");
            return Ok(new
            {
                is_running = false,
                pending_jobs = 0,
                message = "Unable to determine sync status (Redis may be unavailable)"
            });
        }
    }

    [HttpPost("sync-market-categories")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SynchronizeMarketCategories()
    {
        try
        {
            await _synchronizationService.EnqueueTagsCategoriesSyncAsync(HttpContext.RequestAborted);
            return Accepted(new
            {
                started = true,
                message = "Tags and categories synchronization queued"
            });
        }
        catch (RabbitMqUnavailableException ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable while trying to enqueue tags/categories synchronization");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "RabbitMQ unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue tags/categories synchronization");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to enqueue tags/categories synchronization", message = ex.Message });
        }
    }

    [HttpPost("sync-market-series")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SynchronizeMarketSeries()
    {
        try
        {
            await _synchronizationService.EnqueueSeriesSyncAsync(HttpContext.RequestAborted);
            return Accepted(new
            {
                started = true,
                message = "Market series synchronization queued"
            });
        }
        catch (RabbitMqUnavailableException ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable while trying to enqueue market series synchronization");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "RabbitMQ unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue market series synchronization");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to enqueue market series synchronization", message = ex.Message });
        }
    }

    [HttpPost("sync-events")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SynchronizeEvents([FromQuery] string? cursor = null)
    {
        try
        {
            await _synchronizationService.EnqueueEventsSyncAsync(cursor, HttpContext.RequestAborted);
            return Accepted(new
            {
                started = true,
                cursor = cursor ?? "<start>",
                message = "Events synchronization queued"
            });
        }
        catch (RabbitMqUnavailableException ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable while trying to enqueue events synchronization");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "RabbitMQ unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue events synchronization");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to enqueue events synchronization", message = ex.Message });
        }
    }

    [HttpPost("sync-event/{eventTicker}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SynchronizeEventDetail(string eventTicker)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eventTicker))
            {
                return BadRequest(new { error = "Event ticker is required" });
            }

            await _synchronizationService.EnqueueEventDetailSyncAsync(eventTicker, HttpContext.RequestAborted);
            return Accepted(new
            {
                started = true,
                event_ticker = eventTicker,
                message = $"Event detail synchronization queued for: {eventTicker}"
            });
        }
        catch (RabbitMqUnavailableException ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable while trying to enqueue event detail synchronization");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "RabbitMQ unavailable", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue event detail synchronization for {EventTicker}", eventTicker);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to enqueue event detail synchronization", message = ex.Message });
        }
    }

    [HttpPost("sync-orderbook")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SynchronizeOrderbook()
    {
        try
        {
            await _synchronizationService.EnqueueOrderbookSyncAsync(HttpContext.RequestAborted);
            return Accepted(new
            {
                started = true,
                message = "Orderbook synchronization queued for high-priority markets"
            });
        }
        catch (RabbitMqUnavailableException ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable while trying to enqueue orderbook synchronization");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "RabbitMQ unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue orderbook synchronization");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to enqueue orderbook synchronization", message = ex.Message });
        }
    }

    [HttpPost("sync-candlesticks")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SynchronizeCandlesticks()
    {
        try
        {
            await _synchronizationService.EnqueueCandlesticksSyncAsync(HttpContext.RequestAborted);
            return Accepted(new
            {
                started = true,
                message = "Candlesticks synchronization queued for high-priority markets"
            });
        }
        catch (RabbitMqUnavailableException ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable while trying to enqueue candlesticks synchronization");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "RabbitMQ unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue candlesticks synchronization");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to enqueue candlesticks synchronization", message = ex.Message });
        }
    }

    /// <summary>
    /// Cleans up data for finalized/closed markets to free up space.
    /// Finds all tickers with Finalized or Closed status and removes their data from all related tables.
    /// </summary>
    /// <returns>Accepted response with count of markets queued for cleanup</returns>
    [HttpPost("cleanup")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CleanupMarketData()
    {
        try
        {
            var count = await _cleanupService.EnqueueCleanupJobsAsync(HttpContext.RequestAborted);

            return Accepted(new
            {
                started = true,
                markets_queued = count,
                message = $"Cleanup queued for {count} finalized/closed markets"
            });
        }
        catch (RabbitMqUnavailableException ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable while trying to enqueue cleanup jobs");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "RabbitMQ unavailable", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue cleanup jobs");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to enqueue cleanup jobs", message = ex.Message });
        }
    }

    /// <summary>
    /// Cleans up data for a specific market ticker.
    /// Removes data from all related tables (snapshots, candlesticks, orderbook, analytics, highpriority).
    /// </summary>
    /// <param name="tickerId">The market ticker to clean up</param>
    /// <returns>OK response when cleanup is complete</returns>
    [HttpPost("cleanup/{tickerId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CleanupMarketDataForTicker(string tickerId)
    {
        try
        {
            await _cleanupService.CleanupMarketDataAsync(tickerId, HttpContext.RequestAborted);

            return Ok(new
            {
                success = true,
                ticker_id = tickerId,
                message = $"Cleanup completed for ticker: {tickerId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup data for ticker {TickerId}", tickerId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to cleanup market data", message = ex.Message });
        }
    }

    /// <summary>
    /// Cancels all pending MassTransit RabbitMQ jobs by purging all consumer queues.
    /// This removes all queued messages but does not stop currently processing jobs.
    /// </summary>
    /// <returns>Result with counts of purged queues and any errors</returns>
    [HttpPost("cancel-all-jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelAllJobs()
    {
        try
        {
            _logger.LogWarning("Cancel all jobs requested - purging all RabbitMQ queues");

            var result = await _rabbitMqManagementService.PurgeAllQueuesAsync(HttpContext.RequestAborted);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    purged_queues = result.PurgedQueues,
                    skipped_queues = result.SkippedQueues,
                    message = $"Successfully purged {result.PurgedQueues.Count} queues, skipped {result.SkippedQueues.Count} (not found)"
                });
            }
            else
            {
                return Ok(new
                {
                    success = false,
                    purged_queues = result.PurgedQueues,
                    skipped_queues = result.SkippedQueues,
                    errors = result.Errors,
                    message = $"Completed with errors: purged {result.PurgedQueues.Count} queues, {result.Errors.Count} errors"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel all jobs");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to cancel all jobs", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets the current status of all MassTransit RabbitMQ queues.
    /// Shows message counts, consumer counts, and queue health.
    /// </summary>
    /// <returns>Queue statistics for all consumer queues</returns>
    [HttpGet("queue-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetQueueStatus()
    {
        try
        {
            var stats = await _rabbitMqManagementService.GetQueueStatsAsync(HttpContext.RequestAborted);

            var totalMessages = stats.Values.Where(q => q.Exists).Sum(q => q.MessageCount);
            var activeQueues = stats.Values.Count(q => q.Exists);

            return Ok(new
            {
                total_pending_messages = totalMessages,
                active_queues = activeQueues,
                total_queues = stats.Count,
                queues = stats.Select(kvp => new
                {
                    name = kvp.Key,
                    exists = kvp.Value.Exists,
                    messages = kvp.Value.MessageCount,
                    messages_ready = kvp.Value.MessagesReady,
                    messages_unacknowledged = kvp.Value.MessagesUnacknowledged,
                    consumers = kvp.Value.ConsumerCount,
                    error = kvp.Value.Error
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue status");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to get queue status", message = ex.Message });
        }
    }
}
