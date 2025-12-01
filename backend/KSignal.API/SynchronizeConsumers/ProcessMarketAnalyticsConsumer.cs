using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

/// <summary>
/// Consumer that processes analytics for market tickers from market_highpriority.
/// Populates analytics_market_features table with computed features.
/// </summary>
public class ProcessMarketAnalyticsConsumer : IConsumer<ProcessMarketAnalytics>
{
    private readonly AnalyticsService _analyticsService;
    private readonly ILogger<ProcessMarketAnalyticsConsumer> _logger;

    public ProcessMarketAnalyticsConsumer(
        AnalyticsService analyticsService,
        ILogger<ProcessMarketAnalyticsConsumer> logger)
    {
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<ProcessMarketAnalytics> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Processing market analytics for ticker={TickerId}, L1={L1}, L2={L2}, L3={L3}",
            message.TickerId, message.ProcessL1, message.ProcessL2, message.ProcessL3);

        await _analyticsService.ProcessMarketAnalyticsAsync(
            message.TickerId,
            message.ProcessL1,
            message.ProcessL2,
            message.ProcessL3,
            context.CancellationToken);
    }
}
