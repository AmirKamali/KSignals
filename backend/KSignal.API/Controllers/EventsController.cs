using System.Text.RegularExpressions;
using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Models;
using KSignal.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

/// <summary>
/// Controller for events-related endpoints
/// </summary>
[ApiController]
[Route("api/events")]
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
    /// <param name="kalshiService">Market service for caching and filtering</param>
    public EventsController(KalshiClient kalshiClient, ILogger<EventsController> logger, KalshiService kalshiService)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kalshiService = kalshiService ?? throw new ArgumentNullException(nameof(kalshiService));
    }

    private readonly KalshiService _kalshiService;

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

    /// <summary>
    /// Get events with market data
    /// </summary>
    /// <remarks>
    /// Retrieves events joined with latest market snapshot data.
    /// Supports text search across event title, subtitle, yesSubTitle, and noSubTitle.
    /// </remarks>
    /// <param name="category">Filter by category</param>
    /// <param name="tag">Filter by tag</param>
    /// <param name="query">Search text to filter by title, subtitle, yesSubTitle, or noSubTitle</param>
    /// <param name="detailed">Include detailed response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of client events</returns>
    /// <response code="400">Invalid query parameter</response>
    [HttpGet("/api/events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? category = null,
        [FromQuery] string? tag = null,
        [FromQuery] string? query = null,
        [FromQuery] string? close_date_type = null,
        [FromQuery] string? sort_by = null,
        [FromQuery] string? direction = null,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 50,
        [FromQuery] bool detailed = false,
        CancellationToken cancellationToken = default)
    {
        // Validate query parameter for invalid characters
        if (!string.IsNullOrWhiteSpace(query))
        {
            var validationResult = ValidateSearchQuery(query);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { error = "Invalid query parameter", message = validationResult.ErrorMessage });
            }
        }

        // Parse sort_by parameter
        var sortBy = MarketSort.Volume24H;
        if (!string.IsNullOrWhiteSpace(sort_by))
        {
            if (Enum.TryParse<MarketSort>(sort_by, ignoreCase: true, out var parsedSort))
            {
                sortBy = parsedSort;
            }
        }

        // Parse direction parameter
        var sortDirection = SortDirection.Desc;
        if (!string.IsNullOrWhiteSpace(direction))
        {
            if (Enum.TryParse<SortDirection>(direction, ignoreCase: true, out var parsedDirection))
            {
                sortDirection = parsedDirection;
            }
        }

        try
        {
            var eventsResult = await _kalshiService.GetEventsAsync(
                category: category,
                tag: tag,
                query: query,
                closeDateType: close_date_type,
                sortBy: sortBy,
                direction: sortDirection,
                page: page,
                pageSize: page_size,
                cancellationToken: cancellationToken);
            var shaped = MarketResponseMapper.Shape(eventsResult.Markets, detailed).ToList();
            return Ok(new
            {
                count = eventsResult.TotalCount,
                markets = shaped,
                totalPages = eventsResult.TotalPages,
                totalCount = eventsResult.TotalCount,
                currentPage = eventsResult.CurrentPage,
                pageSize = eventsResult.PageSize,
                sort_type = sortBy.ToString(),
                direction = sortDirection.ToString().ToLowerInvariant()
            });
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error during events fetch");
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
            _logger.LogError(ex, "Failed to fetch events");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to fetch events", message = ex.Message });
        }
    }

    /// <summary>
    /// Validates search query for invalid characters
    /// </summary>
    private static (bool IsValid, string? ErrorMessage) ValidateSearchQuery(string query)
    {
        // Check length
        if (query.Length > 200)
        {
            return (false, "Query must be 200 characters or less");
        }

        // Check for SQL injection patterns and invalid characters
        // Allow alphanumeric, spaces, and common punctuation
        var invalidCharsPattern = new Regex(@"[<>{}|\[\]\\^`]|--|;|'|""|\/\*|\*\/", RegexOptions.Compiled);
        if (invalidCharsPattern.IsMatch(query))
        {
            return (false, "Query contains invalid characters");
        }

        // Check for control characters
        if (query.Any(c => char.IsControl(c) && c != ' '))
        {
            return (false, "Query contains invalid control characters");
        }

        return (true, null);
    }


    /// <summary>
    /// Get event details with nested markets
    /// </summary>
    /// <param name="eventTicker">The event ticker identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Event details with nested markets</returns>
    /// <response code="200">Event details retrieved successfully</response>
    /// <response code="404">Event not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("/api/eventDetails")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEventDetails(
        [FromQuery] string eventTicker,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventTicker))
        {
            return BadRequest(new { error = "Event ticker is required" });
        }

        try
        {
            var eventResponse = await _kalshiService.GetEventDetailsAsync(eventTicker);

            if (eventResponse?.Event == null)
            {
                return NotFound(new { error = "Event not found" });
            }

            var clientResponse = MarketResponseMapper.MapEventDetails(eventResponse);
            return Ok(clientResponse);
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error during event details fetch for {EventTicker}", eventTicker);
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
            _logger.LogError(ex, "Failed to fetch event details for {EventTicker}", eventTicker);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to fetch event details", message = ex.Message });
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
