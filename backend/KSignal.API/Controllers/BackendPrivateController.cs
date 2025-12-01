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
    private readonly ILogger<BackendPrivateController> _logger;

    public BackendPrivateController(
        KalshiService kalshiService, 
        SynchronizationService synchronizationService, 
        CleanupService cleanupService,
        ILogger<BackendPrivateController> logger)
    {
        _kalshiService = kalshiService ?? throw new ArgumentNullException(nameof(kalshiService));
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SynchronizeMarketData(
        [FromQuery] string? cursor = null,
        [FromQuery] string? market_ticker_id = null)
    {
        try
        {
            await _synchronizationService.EnqueueMarketSyncAsync(cursor, market_ticker_id, HttpContext.RequestAborted);
            
            if (!string.IsNullOrWhiteSpace(market_ticker_id))
            {
                return Accepted(new
                {
                    started = true,
                    message = $"Market synchronization queued for ticker: {market_ticker_id}",
                    market_ticker_id = market_ticker_id
                });
            }
            
            return Accepted(new
            {
                started = true,
                cursor = cursor ?? "<start>",
                message = "Market synchronization queued"
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
}
