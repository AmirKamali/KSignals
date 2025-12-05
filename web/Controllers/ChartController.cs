using Microsoft.AspNetCore.Mvc;
using web_asp.Services;

namespace web_asp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChartController : ControllerBase
{
    private readonly BackendClient _backendClient;
    private readonly ILogger<ChartController> _logger;

    public ChartController(BackendClient backendClient, ILogger<ChartController> logger)
    {
        _backendClient = backendClient ?? throw new ArgumentNullException(nameof(backendClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<IActionResult> GetChartData([FromQuery] string ticker, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return BadRequest(new { error = "Ticker parameter is required" });
        }

        try
        {
            _logger.LogInformation("Proxying chart data request for ticker: {Ticker}", ticker);
            var chartDataJson = await _backendClient.GetChartDataAsync(ticker);

            return Content(chartDataJson, "application/json");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Chart not found: {Ticker}", ticker);
            return NotFound(new { error = $"Chart data for {ticker} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying chart data for ticker: {Ticker}", ticker);
            return StatusCode(500, new { error = "Failed to fetch chart data" });
        }
    }
}
