using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

public class SynchronizeOrderbookConsumer : IConsumer<SynchronizeOrderbook>
{
    private readonly SynchronizationService _synchronizationService;
    private readonly ILogger<SynchronizeOrderbookConsumer> _logger;

    public SynchronizeOrderbookConsumer(
        SynchronizationService synchronizationService,
        ILogger<SynchronizeOrderbookConsumer> logger)
    {
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<SynchronizeOrderbook> context)
    {
        _logger.LogInformation("Starting orderbook synchronization for high-priority markets");
        await _synchronizationService.SynchronizeOrderbooksAsync(context.CancellationToken);
    }
}
