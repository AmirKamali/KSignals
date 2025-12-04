using KSignals.DTO;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChartsController : ControllerBase
{
    private readonly ILogger<ChartsController> _logger;

    public ChartsController(ILogger<ChartsController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public IActionResult GetChartData([FromQuery] string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return BadRequest(new { error = "Ticker parameter is required" });
        }

        _logger.LogInformation("Generating chart data for ticker: {Ticker}", ticker);

        // Generate random data points for the last 30 days
        var random = new Random(ticker.GetHashCode()); // Use ticker hash as seed for consistent data per ticker
        var dataPoints = new List<ChartDataPoint>();
        var startDate = DateTime.UtcNow.AddDays(-30);

        decimal currentValue = 50m; // Start at 50 cents

        for (int i = 0; i < 30; i++)
        {
            var timestamp = startDate.AddDays(i);

            // Random walk: add a random value between -5 and +5
            var change = (decimal)(random.NextDouble() * 10 - 5);
            currentValue = Math.Max(10m, Math.Min(90m, currentValue + change)); // Keep between 10 and 90

            dataPoints.Add(new ChartDataPoint
            {
                Timestamp = timestamp,
                Value = Math.Round(currentValue, 2)
            });
        }

        var response = new ChartDataResponse
        {
            Ticker = ticker,
            DataPoints = dataPoints
        };

        return Ok(response);
    }
}
