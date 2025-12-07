using KSignal.API.Messaging;
using KSignal.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KSignal.API.SynchronizeConsumers;

public class SynchronizeMarketDataConsumer : IConsumer<SynchronizeMarketData>
{
    private readonly SynchronizationService _synchronizationService;
    private readonly IRedisCacheService _redisCacheService;
    private readonly ILogger<SynchronizeMarketDataConsumer> _logger;

    private const string MarketSyncLockKey = "sync:market-snapshots:lock";
    private const string MarketSyncCounterKey = "sync:market-snapshots:pending";

    public SynchronizeMarketDataConsumer(
        SynchronizationService synchronizationService,
        IRedisCacheService redisCacheService,
        ILogger<SynchronizeMarketDataConsumer> logger)
    {
        _synchronizationService = synchronizationService ?? throw new ArgumentNullException(nameof(synchronizationService));
        _redisCacheService = redisCacheService ?? throw new ArgumentNullException(nameof(redisCacheService));
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
        finally
        {
            // Decrement the job counter
            var remainingJobs = await _redisCacheService.DecrementCounterAsync(MarketSyncCounterKey, context.CancellationToken);

            _logger.LogDebug("Market sync job completed, remaining jobs: {RemainingJobs}", remainingJobs);

            // If all jobs are complete (counter reached 0 or below), release the lock
            if (remainingJobs <= 0)
            {
                var lockReleased = await _redisCacheService.ReleaseLockAsync(MarketSyncLockKey, context.CancellationToken);

                if (lockReleased)
                {
                    _logger.LogInformation("All market sync jobs completed, lock released successfully");
                }
                else
                {
                    _logger.LogWarning("Attempted to release market sync lock but it was not found (may have expired)");
                }

                // Reset counter to 0 for next sync operation
                await _redisCacheService.SetCounterAsync(MarketSyncCounterKey, 0, context.CancellationToken);
            }
        }
    }
}
