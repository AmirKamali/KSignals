using KSignal.API.Data;
using KSignal.API.Models;
using Microsoft.Extensions.Logging;

namespace KSignal.API.Services;

public interface ISyncLogService
{
    /// <summary>
    /// Logs a synchronization job enqueue operation
    /// </summary>
    /// <param name="eventName">Name of the sync event</param>
    /// <param name="numbersEnqueued">Number of jobs/messages enqueued</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LogSyncEventAsync(string eventName, int numbersEnqueued, CancellationToken cancellationToken = default);
}

public class SyncLogService : ISyncLogService
{
    private readonly KalshiDbContext _dbContext;
    private readonly ILogger<SyncLogService> _logger;

    public SyncLogService(
        KalshiDbContext dbContext,
        ILogger<SyncLogService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogSyncEventAsync(string eventName, int numbersEnqueued, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("Event name cannot be null or empty", nameof(eventName));
        }

        if (numbersEnqueued < 0)
        {
            throw new ArgumentException("Numbers enqueued cannot be negative", nameof(numbersEnqueued));
        }

        try
        {
            var syncLog = new SyncLog
            {
                Id = Guid.NewGuid(),
                EventName = eventName,
                NumbersEnqueued = numbersEnqueued,
                LogDate = DateTime.UtcNow
            };

            await _dbContext.SyncLogs.AddAsync(syncLog, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Logged sync event: {EventName}, Enqueued: {NumbersEnqueued}", eventName, numbersEnqueued);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log sync event: {EventName}, Enqueued: {NumbersEnqueued}", eventName, numbersEnqueued);
            // Don't rethrow - logging should not break the sync operation
        }
    }
}
