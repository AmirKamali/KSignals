using Kalshi.Api.Client;
using KSignal.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

/// <summary>
/// Private API for data source refresh
/// </summary>
[ApiController]
[Route("api/private/data-source/[controller]")]
[Produces("application/json")]
public class DataSourceController : ControllerBase
{
    private readonly KalshiService _kalshiService;
    private readonly ILogger<DataSourceController> _logger;

    public DataSourceController(KalshiService kalshiService, ILogger<DataSourceController> logger)
    {
        _kalshiService = kalshiService ?? throw new ArgumentNullException(nameof(kalshiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("refresh-market")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshMarket([FromQuery] string? category = null, [FromQuery] string? tag = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _kalshiService.RefreshMarketCategoriesAsync(category, tag, cancellationToken);
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

    [HttpGet("markets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMarkets(
        [FromQuery] string? category = null,
        [FromQuery] string? tag = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var markets = await _kalshiService.GetMarketsAsync(category, tag, cancellationToken);
            var safePageSize = Math.Max(1, pageSize);
            var safePage = Math.Max(1, page);
            var totalCount = markets.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)safePageSize);
            var paged = markets.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList();
            var shaped = MarketResponseMapper.Shape(paged).ToList();

            return Ok(new
            {
                count = totalCount,
                totalPages,
                currentPage = safePage,
                pageSize = safePageSize,
                markets = shaped
            });
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error during markets fetch");
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
            _logger.LogError(ex, "Failed to fetch markets");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to fetch markets", message = ex.Message });
        }
    }
}
