using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

public class SynchronizeEventDetailConsumer : IConsumer<SynchronizeEventDetail>
{
    private readonly SynchronizationService _synchronizationService;
    private readonly ILogger<SynchronizeEventDetailConsumer> _logger;

    public SynchronizeEventDetailConsumer(
        SynchronizationService synchronizationService,
        ILogger<SynchronizeEventDetailConsumer> logger)
    {
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<SynchronizeEventDetail> context)
    {
        try
        {
            await _synchronizationService.SynchronizeEventDetailAsync(context.Message, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during event detail synchronization for {EventTicker}", context.Message.EventTicker);
        }
    }
}

