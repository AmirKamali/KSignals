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
    private readonly ILogger<BackendPrivateController> _logger;

    public BackendPrivateController(KalshiService kalshiService, SynchronizationService synchronizationService, ILogger<BackendPrivateController> logger)
    {
        _kalshiService = kalshiService ?? throw new ArgumentNullException(nameof(kalshiService));
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Note: These endpoints are disabled because RefreshService is currently commented out
    // [HttpPost("refresh-series-categories-tags")]
    // [HttpGet("category-refresh-status")]
    // [HttpPost("refresh-market-data")]
    // [HttpGet("cache-market-status")]
    // [HttpPost("refresh-today-market")]

    [HttpPost("synchronize-market-data")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SynchronizeMarketData([FromQuery] string? cursor = null)
    {
        try
        {
            await _synchronizationService.EnqueueMarketSyncAsync(cursor, HttpContext.RequestAborted);
            return Accepted(new
            {
                started = true,
                cursor = cursor ?? "<start>"
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
}
