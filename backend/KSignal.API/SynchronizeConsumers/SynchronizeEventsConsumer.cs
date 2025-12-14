using Kalshi.Api.Client;
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
        try
        {
            await _synchronizationService.SynchronizeEventsAsync(context.Message, context.CancellationToken);
        }
        catch (RateLimitExceededException rateLimitEx)
        {
            // Rate limit exceeded - log warning and gracefully complete the job without retry
            _logger.LogWarning(rateLimitEx,
                "Rate limit exceeded during events synchronization. Job will be removed without retry. " +
                "Message: {Message}",
                rateLimitEx.Message);

            // Job will be gracefully completed without retry
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during events synchronization");
            throw;
        }
    }
}
