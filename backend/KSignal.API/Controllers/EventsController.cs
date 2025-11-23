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
    /// Get a list of events
    /// </summary>
    /// <remarks>
    /// Retrieves a list of events from the Kalshi API with optional filtering.
    /// Supports filtering by status, series ticker, minimum close timestamp, and pagination.
    /// Maximum limit is 200.
    /// </remarks>
    /// <param name="limit">Maximum number of events to return (default: 100, maximum: 200)</param>
    /// <param name="cursor">Cursor for pagination</param>
    /// <param name="withNestedMarkets">Whether to include nested markets in the response</param>
    /// <param name="withMilestones">Whether to include milestones in the response</param>
    /// <param name="status">Filter by event status (e.g., "open", "closed")</param>
    /// <param name="seriesTicker">Filter by series ticker</param>
    /// <param name="minCloseTs">Filter by minimum close timestamp (Unix timestamp)</param>
    /// <returns>A list of events</returns>
    /// <response code="200">Events retrieved successfully</response>
    /// <response code="400">Bad request - invalid parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(GetEventsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetEventsResponse>> GetEvents(
        [FromQuery] int limit = 200,
        [FromQuery] string? cursor = null,
        [FromQuery] bool? withNestedMarkets = null,
        [FromQuery] bool? withMilestones = null,
        [FromQuery] string? status = "open",
        [FromQuery] string? seriesTicker = null,
        [FromQuery] long? minCloseTs = null)
    {
        try
        {
            // Validate limit parameter
            if (limit < 1)
            {
                return BadRequest(new { error = "Limit must be greater than 0", limit });
            }
            
            if (limit > 200)
            {
                return BadRequest(new { error = "Limit cannot exceed 200. Maximum value is 200.", limit });
            }
            
            _logger.LogInformation("Fetching events from Kalshi API with filters: limit={Limit}, status={Status}, seriesTicker={SeriesTicker}", 
                limit, status, seriesTicker);
            
            var response = await _kalshiClient.Events.GetEventsAsync(
                limit: limit,
                cursor: cursor,
                withNestedMarkets: withNestedMarkets,
                withMilestones: withMilestones,
                status: status,
                seriesTicker: seriesTicker,
                minCloseTs: minCloseTs);
            
            _logger.LogInformation("Successfully retrieved {Count} events", response?.Events?.Count ?? 0);
            return Ok(response);
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "API error fetching events from Kalshi API. Status: {StatusCode}", apiEx.ErrorCode);
            return BuildApiErrorResponse(apiEx, "Kalshi API error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching events from Kalshi API");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve events", message = ex.Message });
        }
    }

    /// <summary>
    /// Get a single event by ticker
    /// </summary>
    /// <remarks>
    /// Retrieves detailed information about a specific event identified by its ticker.
    /// </remarks>
    /// <param name="eventTicker">The event ticker identifier</param>
    /// <param name="withNestedMarkets">Whether to include nested markets in the response</param>
    /// <returns>Event details</returns>
    /// <response code="200">Event retrieved successfully</response>
    /// <response code="404">Event not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{eventTicker}")]
    [ProducesResponseType(typeof(GetEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetEventResponse>> GetEvent(
        [FromRoute] string eventTicker,
        [FromQuery] bool? withNestedMarkets = null)
    {
        try
        {
            _logger.LogInformation("Fetching event {EventTicker} from Kalshi API", eventTicker);
            
            var response = await _kalshiClient.Events.GetEventAsync(
                eventTicker: eventTicker,
                withNestedMarkets: withNestedMarkets);
            
            _logger.LogInformation("Successfully retrieved event {EventTicker}", eventTicker);
            return Ok(response);
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "API error fetching event {EventTicker} from Kalshi API. Status: {StatusCode}", eventTicker, apiEx.ErrorCode);

            if (apiEx.ErrorCode == StatusCodes.Status404NotFound)
            {
                return NotFound(new { error = "Event not found", eventTicker, message = apiEx.Message });
            }

            return BuildApiErrorResponse(apiEx, "Kalshi API error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching event {EventTicker} from Kalshi API", eventTicker);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve event", message = ex.Message });
        }
    }

    /// <summary>
    /// Get event metadata
    /// </summary>
    /// <remarks>
    /// Retrieves metadata information for a specific event identified by its ticker.
    /// </remarks>
    /// <param name="eventTicker">The event ticker identifier</param>
    /// <returns>Event metadata</returns>
    /// <response code="200">Event metadata retrieved successfully</response>
    /// <response code="404">Event not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{eventTicker}/metadata")]
    [ProducesResponseType(typeof(GetEventMetadataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetEventMetadataResponse>> GetEventMetadata([FromRoute] string eventTicker)
    {
        try
        {
            _logger.LogInformation("Fetching metadata for event {EventTicker} from Kalshi API", eventTicker);
            
            var response = await _kalshiClient.Events.GetEventMetadataAsync(eventTicker: eventTicker);
            
            _logger.LogInformation("Successfully retrieved metadata for event {EventTicker}", eventTicker);
            return Ok(response);
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "API error fetching metadata for event {EventTicker} from Kalshi API. Status: {StatusCode}", eventTicker, apiEx.ErrorCode);

            if (apiEx.ErrorCode == StatusCodes.Status404NotFound)
            {
                return NotFound(new { error = "Event not found", eventTicker, message = apiEx.Message });
            }

            return BuildApiErrorResponse(apiEx, "Kalshi API error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching metadata for event {EventTicker} from Kalshi API", eventTicker);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve event metadata", message = ex.Message });
        }
    }

    /// <summary>
    /// Get multivariate events
    /// </summary>
    /// <remarks>
    /// Retrieves a list of multivariate events with optional filtering by series ticker or collection ticker.
    /// </remarks>
    /// <param name="limit">Maximum number of events to return (default: API default)</param>
    /// <param name="cursor">Cursor for pagination</param>
    /// <param name="seriesTicker">Filter by series ticker</param>
    /// <param name="collectionTicker">Filter by collection ticker</param>
    /// <param name="withNestedMarkets">Whether to include nested markets in the response</param>
    /// <returns>A list of multivariate events</returns>
    /// <response code="200">Multivariate events retrieved successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("multivariate")]
    [ProducesResponseType(typeof(GetMultivariateEventsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetMultivariateEventsResponse>> GetMultivariateEvents(
        [FromQuery] int? limit = null,
        [FromQuery] string? cursor = null,
        [FromQuery] string? seriesTicker = null,
        [FromQuery] string? collectionTicker = null,
        [FromQuery] bool? withNestedMarkets = null)
    {
        try
        {
            _logger.LogInformation("Fetching multivariate events from Kalshi API with filters: limit={Limit}, seriesTicker={SeriesTicker}, collectionTicker={CollectionTicker}", 
                limit, seriesTicker, collectionTicker);
            
            var response = await _kalshiClient.Events.GetMultivariateEventsAsync(
                limit: limit,
                cursor: cursor,
                seriesTicker: seriesTicker,
                collectionTicker: collectionTicker,
                withNestedMarkets: withNestedMarkets);
            
            _logger.LogInformation("Successfully retrieved multivariate events");
            return Ok(response);
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "API error fetching multivariate events from Kalshi API. Status: {StatusCode}", apiEx.ErrorCode);
            return BuildApiErrorResponse(apiEx, "Kalshi API error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching multivariate events from Kalshi API");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve multivariate events", message = ex.Message });
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
