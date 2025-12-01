using KSignal.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

/// <summary>
/// API for analytics feature processing
/// </summary>
[ApiController]
[Route("api/analytics/")]
[Produces("application/json")]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsService _analyticsService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        AnalyticsService analyticsService,
        ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Triggers analytics feature processing for all high-priority markets.
    /// Reads tickerIds from market_highpriority and enqueues jobs to populate analytics_market_features.
    /// </summary>
    /// <returns>Accepted response with job status</returns>
    [HttpPost("market_feature")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessMarketFeatures()
    {
        try
        {
            await _analyticsService.EnqueueMarketAnalyticsAsync(HttpContext.RequestAborted);
            
            return Accepted(new
            {
                started = true,
                message = "Market analytics feature processing queued for high-priority markets"
            });
        }
        catch (RabbitMqUnavailableException ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable while trying to enqueue analytics processing");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new 
            { 
                error = "RabbitMQ unavailable", 
                message = ex.Message 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue market analytics processing");
            return StatusCode(StatusCodes.Status500InternalServerError, new 
            { 
                error = "Failed to enqueue market analytics processing", 
                message = ex.Message 
            });
        }
    }

    /// <summary>
    /// Triggers analytics feature processing for a specific market ticker.
    /// </summary>
    /// <param name="tickerId">The market ticker to process analytics for</param>
    /// <param name="processL1">Whether to process L1 analytics (basic features)</param>
    /// <param name="processL2">Whether to process L2 analytics (volatility/returns)</param>
    /// <param name="processL3">Whether to process L3 analytics (advanced metrics)</param>
    /// <returns>Accepted response with job status</returns>
    [HttpPost("market_feature/{tickerId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessMarketFeatureForTicker(
        string tickerId,
        [FromQuery] bool processL1 = true,
        [FromQuery] bool processL2 = true,
        [FromQuery] bool processL3 = true)
    {
        try
        {
            // Directly process the analytics for a single ticker
            await _analyticsService.ProcessMarketAnalyticsAsync(
                tickerId, 
                processL1, 
                processL2, 
                processL3, 
                HttpContext.RequestAborted);
            
            return Accepted(new
            {
                started = true,
                tickerId = tickerId,
                message = $"Market analytics feature processed for ticker: {tickerId}",
                levels = new { l1 = processL1, l2 = processL2, l3 = processL3 }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process market analytics for ticker {TickerId}", tickerId);
            return StatusCode(StatusCodes.Status500InternalServerError, new 
            { 
                error = "Failed to process market analytics", 
                message = ex.Message 
            });
        }
    }
}
