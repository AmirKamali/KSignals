using System.Runtime.CompilerServices;
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
    /// <param name="callerFilePath">Automatically populated with caller's file path</param>
    Task LogSyncEventAsync(
        string eventName,
        int numbersEnqueued,
        CancellationToken cancellationToken = default,
        LogType type = LogType.Info,
        [CallerFilePath] string callerFilePath = "");
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

    public async Task LogSyncEventAsync(
        string eventName,
        int numbersEnqueued,
        CancellationToken cancellationToken = default,
        LogType type = LogType.Info,
        [CallerFilePath] string callerFilePath = "")
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("Event name cannot be null or empty", nameof(eventName));
        }

        if (numbersEnqueued < 0)
        {
            throw new ArgumentException("Numbers enqueued cannot be negative", nameof(numbersEnqueued));
        }

        // Extract component name from file path
        var component = ExtractComponentName(callerFilePath);

        // Determine if running in Debug mode
        var isDebug = IsDebugMode();

        try
        {
            var syncLog = new SyncLog
            {
                EventName = eventName,
                NumbersEnqueued = numbersEnqueued,
                Type = type.ToString(),
                Component = component,
                IsDebug = isDebug,
                LogDate = DateTime.UtcNow
            };

            await _dbContext.SyncLogs.AddAsync(syncLog, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't rethrow - logging should not break the sync operation
        }
    }

    /// <summary>
    /// Determines if the application is running in Debug mode
    /// </summary>
    /// <returns>True if Debug mode, False if Release mode</returns>
    private static bool IsDebugMode()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// Extracts the component/class name from the caller's file path
    /// </summary>
    /// <param name="filePath">Full file path from CallerFilePath attribute</param>
    /// <returns>Component name (file name without extension)</returns>
    private static string ExtractComponentName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "Unknown";
        }

        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            return string.IsNullOrWhiteSpace(fileName) ? "Unknown" : fileName;
        }
        catch
        {
            return "Unknown";
        }
    }
}
