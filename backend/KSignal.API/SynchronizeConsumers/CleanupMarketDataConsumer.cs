using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

/// <summary>
/// Consumer that processes cleanup jobs for individual market tickers.
/// Deletes data from all tables referencing the ticker.
/// </summary>
public class CleanupMarketDataConsumer : IConsumer<CleanupMarketData>
{
    private readonly CleanupService _cleanupService;
    private readonly ILogger<CleanupMarketDataConsumer> _logger;

    public CleanupMarketDataConsumer(
        CleanupService cleanupService,
        ILogger<CleanupMarketDataConsumer> logger)
    {
        _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<CleanupMarketData> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing cleanup for ticker={TickerId}", message.TickerId);

        await _cleanupService.CleanupMarketDataAsync(message.TickerId, context.CancellationToken);
    }
}
