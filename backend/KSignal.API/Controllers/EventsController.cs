using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

/// <summary>
/// Controller for events-related endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly KalshiClient _kalshiClient;
    private readonly ILogger<EventsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventsController"/> class.
    /// </summary>
    /// <param name="kalshiClient">The Kalshi API client</param>
    /// <param name="logger">The logger</param>
    public EventsController(KalshiClient kalshiClient, ILogger<EventsController> logger)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    /// <summary>
    /// Get tags organized by series categories
    /// </summary>
    /// <remarks>
    /// Retrieves tags organized by series categories from the Kalshi API.
    /// This endpoint returns a mapping of series categories to their associated tags,
    /// which can be used for filtering and search functionality.
    /// </remarks>
    /// <returns>A mapping of series categories to their associated tags</returns>
    /// <response code="200">Tags retrieved successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(GetTagsForSeriesCategoriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetTagsForSeriesCategoriesResponse>> GetTagsByCategories()
    {
        try
        {
            _logger.LogInformation("Fetching tags by categories from Kalshi API");

            var response = await _kalshiClient.Search.GetTagsForSeriesCategoriesAsync();

            _logger.LogInformation("Successfully retrieved tags by categories");
            return Ok(response);
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "API error fetching tags by categories from Kalshi API. Status: {StatusCode}", apiEx.ErrorCode);

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
            _logger.LogError(ex, "Error fetching tags by categories from Kalshi API");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve tags by categories", message = ex.Message });
        }
    }

    [HttpGet("/api/markets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMarkets([FromQuery] string? category = null, [FromQuery] string? tag = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var markets = await _kalshiService.GetMarketsAsync(category, tag, cancellationToken);
            return Ok(new { count = markets.Count, markets });
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


    /// <summary>
    /// Creates a consistent API error response for Kalshi API exceptions.
    /// </summary>
    /// <param name="apiEx">The Kalshi API exception</param>
    /// <param name="error">Friendly error label for clients</param>
    /// <returns>Formatted error response with appropriate status code</returns>
    private ObjectResult BuildApiErrorResponse(ApiException apiEx, string error)
    {
        var statusCode = apiEx.ErrorCode >= 400 && apiEx.ErrorCode < 600
            ? apiEx.ErrorCode
            : StatusCodes.Status502BadGateway;

        return StatusCode(statusCode, new
        {
            error,
            message = apiEx.Message,
            statusCode = apiEx.ErrorCode,
            details = apiEx.ErrorContent
        });
    }
}
