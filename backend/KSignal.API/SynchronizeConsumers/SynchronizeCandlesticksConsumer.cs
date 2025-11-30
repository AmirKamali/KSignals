using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

public class SynchronizeCandlesticksConsumer : IConsumer<SynchronizeCandlesticks>
{
    private readonly SynchronizationService _synchronizationService;
    private readonly ILogger<SynchronizeCandlesticksConsumer> _logger;

    public SynchronizeCandlesticksConsumer(
        SynchronizationService synchronizationService,
        ILogger<SynchronizeCandlesticksConsumer> logger)
    {
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<SynchronizeCandlesticks> context)
    {
        _logger.LogInformation("Starting candlesticks synchronization");
        await _synchronizationService.SynchronizeCandlesticksAsync(context.CancellationToken);
    }
}
