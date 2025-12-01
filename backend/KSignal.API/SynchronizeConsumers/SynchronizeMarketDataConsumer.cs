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
        if (!string.IsNullOrWhiteSpace(context.Message.MarketTickerId))
        {
            _logger.LogInformation("Starting single market synchronization for ticker={TickerId}", context.Message.MarketTickerId);
        }
        else if (!string.IsNullOrWhiteSpace(context.Message.Category))
        {
            _logger.LogInformation("Starting market synchronization for category={Category} cursor={Cursor}", 
                context.Message.Category, context.Message.Cursor ?? "<start>");
        }
        else
        {
            _logger.LogWarning("Market synchronization message missing both MarketTickerId and Category");
        }
        await _synchronizationService.SynchronizeMarketDataAsync(context.Message, context.CancellationToken);
    }
}
