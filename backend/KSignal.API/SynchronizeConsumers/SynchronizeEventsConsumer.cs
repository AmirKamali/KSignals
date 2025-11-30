using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

public class SynchronizeEventsConsumer : IConsumer<SynchronizeEvents>
{
    private readonly SynchronizationService _synchronizationService;
    private readonly ILogger<SynchronizeEventsConsumer> _logger;

    public SynchronizeEventsConsumer(
        SynchronizationService synchronizationService,
        ILogger<SynchronizeEventsConsumer> logger)
    {
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<SynchronizeEvents> context)
    {
        _logger.LogInformation("Starting events synchronization (cursor={Cursor})", context.Message.Cursor ?? "<start>");
        await _synchronizationService.SynchronizeEventsAsync(context.Message, context.CancellationToken);
    }
}
