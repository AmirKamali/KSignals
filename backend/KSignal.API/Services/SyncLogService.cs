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
    /// <param name="type">Log type/severity level (default: Info)</param>
    Task LogSyncEventAsync(string eventName, int numbersEnqueued, CancellationToken cancellationToken = default, LogType type = LogType.Info);
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

    public async Task LogSyncEventAsync(string eventName, int numbersEnqueued, CancellationToken cancellationToken = default, LogType type = LogType.Info)
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
                EventName = eventName,
                NumbersEnqueued = numbersEnqueued,
                Type = type.ToString(),
                LogDate = DateTime.UtcNow
            };

            await _dbContext.SyncLogs.AddAsync(syncLog, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Logged sync event: {EventName}, Type: {Type}, Enqueued: {NumbersEnqueued}", eventName, type, numbersEnqueued);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log sync event: {EventName}, Type: {Type}, Enqueued: {NumbersEnqueued}", eventName, type, numbersEnqueued);
            // Don't rethrow - logging should not break the sync operation
        }
    }
}
