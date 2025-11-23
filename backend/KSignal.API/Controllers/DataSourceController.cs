using Kalshi.Api.Client;
using KSignal.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<IActionResult> RefreshMarket(CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _kalshiService.RefreshMarketCategoriesAsync(cancellationToken);
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
}

