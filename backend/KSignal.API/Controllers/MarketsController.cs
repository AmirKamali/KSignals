using Kalshi.Api.Client;
using KSignal.API.Models;
using KSignal.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

[ApiController]
[Route("api/marketDetails")]
[Produces("application/json")]
public class MarketsController : ControllerBase
{
    private readonly KalshiService _kalshiService;
    private readonly ILogger<MarketsController> _logger;

    public MarketsController(KalshiService kalshiService, ILogger<MarketsController> logger)
    {
        _kalshiService = kalshiService ?? throw new ArgumentNullException(nameof(kalshiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetMarketDetails([FromQuery] string? tickerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tickerId))
        {
            return BadRequest(new { error = "tickerId is required" });
        }

        try
        {
            var market = await _kalshiService.GetMarketDetailsAsync(tickerId);
            if (market == null)
            {
                return NotFound(new { error = "Market not found", tickerId });
            }

            var shaped = MarketResponseMapper.ToResponse(market, detailed: true, category: null, tags: Array.Empty<string>());
            return Ok(new { market = shaped });
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error fetching market details for {TickerId}", tickerId);
            var statusCode = apiEx.ErrorCode >= 400 && apiEx.ErrorCode < 600
                ? apiEx.ErrorCode
                : StatusCodes.Status502BadGateway;

            return StatusCode(statusCode, new
            {
                error = "Kalshi API error",
                message = apiEx.Message,
                statusCode = apiEx.ErrorCode,
                details = apiEx.ErrorContent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch market details for {TickerId}", tickerId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to fetch market details", message = ex.Message });
        }
    }
}
