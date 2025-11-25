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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshMarket([FromQuery] string? category = null, [FromQuery] string? tag = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _refreshService.RefreshMarketCategoriesAsync(category, tag, cancellationToken);
            return Ok(new { updated, refreshedAt = DateTime.UtcNow });
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error during market category refresh");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Kalshi API error",
                message = apiEx.Message,
                statusCode = apiEx.ErrorCode,
                details = apiEx.ErrorContent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh market categories");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to refresh market categories", message = ex.Message });
        }
    }

    [HttpPost("refresh-market-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CacheMarketData([FromQuery] string? category = null, [FromQuery] string? tag = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting market data cache request for category={Category}, tag={Tag}", category, tag);

            var cachedCount = await _refreshService.CacheMarketDataAsync(category, tag, cancellationToken);

            _logger.LogInformation("Successfully cached {Count} markets", cachedCount);

            return Ok(new
            {
                cached = cachedCount,
                cachedAt = DateTime.UtcNow,
                category,
                tag
            });
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error during market data caching");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Kalshi API error",
                message = apiEx.Message,
                statusCode = apiEx.ErrorCode,
                details = apiEx.ErrorContent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache market data");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to cache market data", message = ex.Message });
        }
    }
}
