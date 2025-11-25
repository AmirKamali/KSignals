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
    private readonly RefreshService _refreshService;
    private readonly ILogger<BackendPrivateController> _logger;

    public BackendPrivateController(KalshiService kalshiService, RefreshService refreshService, ILogger<BackendPrivateController> logger)
    {
        _kalshiService = kalshiService ?? throw new ArgumentNullException(nameof(kalshiService));
        _refreshService = refreshService ?? throw new ArgumentNullException(nameof(refreshService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("refresh-series-categories-tags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult RefreshCategoriesTagsSeriers([FromQuery] string? category = null, [FromQuery] string? tag = null)
    {
        try
        {
            _logger.LogInformation("Starting category refresh request for category={Category}, tag={Tag}", category, tag);

            var started = _refreshService.RefreshMarketCategoriesAsync(category, tag);

            if (!started)
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    error = "Category refresh is already processing",
                    message = "Another category refresh operation is currently in progress. Check the status endpoint for progress."
                });
            }

            _logger.LogInformation("Category refresh task started in background");

            return Ok(new
            {
                started = true,
                message = "Category refresh task started in background",
                startedAt = DateTime.UtcNow,
                category,
                tag
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start category refresh task");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to start category refresh task", message = ex.Message });
        }
    }

    [HttpGet("category-refresh-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetCategoryRefreshStatus()
    {
        try
        {
            var status = _refreshService.GetCategoryRefreshStatus();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get category refresh status");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to get category refresh status", message = ex.Message });
        }
    }

    [HttpPost("refresh-market-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult CacheMarketData([FromQuery] string? category = null, [FromQuery] string? tag = null)
    {
        try
        {
            _logger.LogInformation("Starting market data cache request for category={Category}, tag={Tag}", category, tag);

            var started = _refreshService.CacheMarketDataAsync(category, tag);

            if (!started)
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    error = "Cache market data is already processing",
                    message = "Another cache operation is currently in progress. Check the status endpoint for progress."
                });
            }

            _logger.LogInformation("Market data cache task started in background");

            return Ok(new
            {
                started = true,
                message = "Cache market data task started in background",
                startedAt = DateTime.UtcNow,
                category,
                tag
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start cache market data task");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to start cache market data task", message = ex.Message });
        }
    }

    [HttpGet("cache-market-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetCacheMarketStatus()
    {
        try
        {
            var status = _refreshService.GetCacheMarketStatus();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache market status");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to get cache market status", message = ex.Message });
        }
    }
}
