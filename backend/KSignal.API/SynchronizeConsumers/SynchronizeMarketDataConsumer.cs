using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

public class SynchronizeMarketDataConsumer : IConsumer<SynchronizeMarketData>
{
    private readonly SynchronizationService _synchronizationService;
    private readonly ILogger<SynchronizeMarketDataConsumer> _logger;

    public SynchronizeMarketDataConsumer(
        SynchronizationService synchronizationService,
        ILogger<SynchronizeMarketDataConsumer> logger)
    {
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<SynchronizeMarketData> context)
    {
        try
        {
            await _synchronizationService.SynchronizeMarketDataAsync(context.Message, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during market data synchronization");
        }
    }
}
