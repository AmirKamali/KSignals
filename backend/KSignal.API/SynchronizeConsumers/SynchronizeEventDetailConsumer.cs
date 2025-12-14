using Kalshi.Api.Client;
using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

public class SynchronizeEventDetailConsumer : IConsumer<Batch<SynchronizeEventDetail>>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SynchronizeEventDetailConsumer> _logger;

    public SynchronizeEventDetailConsumer(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SynchronizeEventDetailConsumer> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<Batch<SynchronizeEventDetail>> context)
    {
        var batch = context.Message;
        _logger.LogInformation("Processing batch of {Count} event detail synchronization messages", batch.Length);

        var tasks = new List<Task>();
        foreach (var message in batch)
        {
            tasks.Add(ProcessMessageAsync(message.Message, context.CancellationToken));
        }

        await Task.WhenAll(tasks);

        _logger.LogInformation("Completed batch of {Count} event detail synchronization messages", batch.Length);
    }

    private async Task ProcessMessageAsync(SynchronizeEventDetail message, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var synchronizationService = scope.ServiceProvider.GetRequiredService<SynchronizationService>();
            await synchronizationService.SynchronizeEventDetailAsync(message, cancellationToken);
        }
        catch (RateLimitExceededException rateLimitEx)
        {
            // Rate limit exceeded - log warning and gracefully complete the job without retry
            _logger.LogWarning(rateLimitEx,
                "Rate limit exceeded during event detail synchronization. Job will be removed without retry. " +
                "EventTicker: {EventTicker}, Message: {Message}",
                message.EventTicker,
                rateLimitEx.Message);

            // Job will be gracefully completed without retry
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during event detail synchronization for {EventTicker}", message.EventTicker);
        }
    }
}

