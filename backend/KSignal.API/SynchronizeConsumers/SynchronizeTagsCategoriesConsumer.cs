using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

public class SynchronizeTagsCategoriesConsumer : IConsumer<SynchronizeTagsCategories>
{
    private readonly SynchronizationService _synchronizationService;
    private readonly ILogger<SynchronizeTagsCategoriesConsumer> _logger;

    public SynchronizeTagsCategoriesConsumer(
        SynchronizationService synchronizationService,
        ILogger<SynchronizeTagsCategoriesConsumer> logger)
    {
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<SynchronizeTagsCategories> context)
    {
        _logger.LogInformation("Starting tags and categories synchronization");
        await _synchronizationService.SynchronizeTagsCategoriesAsync(context.CancellationToken);
    }
}
