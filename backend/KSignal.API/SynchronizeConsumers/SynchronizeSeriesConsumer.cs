using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

public class SynchronizeSeriesConsumer : IConsumer<SynchronizeSeries>
{
    private readonly SynchronizationService _synchronizationService;
    private readonly ILogger<SynchronizeSeriesConsumer> _logger;

    public SynchronizeSeriesConsumer(
        SynchronizationService synchronizationService,
        ILogger<SynchronizeSeriesConsumer> logger)
    {
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<SynchronizeSeries> context)
    {
        _logger.LogInformation("Starting series synchronization");
        await _synchronizationService.SynchronizeSeriesAsync(context.CancellationToken);
    }
}
