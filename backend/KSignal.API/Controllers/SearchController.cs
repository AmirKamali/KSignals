using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

/// <summary>
/// Controller for search-related endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly KalshiClient _kalshiClient;
    private readonly ILogger<SearchController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchController"/> class.
    /// </summary>
    /// <param name="kalshiClient">The Kalshi API client</param>
    /// <param name="logger">The logger</param>
    public SearchController(KalshiClient kalshiClient, ILogger<SearchController> logger)
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
}
