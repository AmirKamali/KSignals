using Kalshi.Api.Client;
using KSignal.API.Models;
using KSignal.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace KSignal.API.Controllers;

[ApiController]
[Route("api/markets")]
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
}
