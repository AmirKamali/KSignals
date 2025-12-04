using KSignal.API.Services;
using KSignals.DTO;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChartsController : ControllerBase
{
    private readonly ChartService _chartService;
    private readonly ILogger<ChartsController> _logger;

    public ChartsController(ChartService chartService, ILogger<ChartsController> logger)
    {
        _chartService = chartService ?? throw new ArgumentNullException(nameof(chartService));
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
            _logger.LogInformation("Fetching chart data for ticker: {Ticker}", ticker);
            var chartData = await _chartService.GetChartDataAsync(ticker, cancellationToken);
            return Ok(chartData);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Market not found: {Ticker}", ticker);
            return NotFound(new { error = $"Market {ticker} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching chart data for ticker: {Ticker}", ticker);
            return StatusCode(500, new { error = "Failed to fetch chart data" });
        }
    }
}
