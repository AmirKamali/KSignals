using Microsoft.Extensions.Logging;

namespace KSignal.API.Services;

public interface ILockService
{
    /// <summary>
    /// Acquires a distributed lock and initializes its associated counter to 0.
    /// </summary>
    /// <param name="lockKey">The key for the distributed lock</param>
    /// <param name="counterKey">The key for the job counter</param>
    /// <param name="expiration">Lock expiration duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock was acquired, false otherwise</returns>
    Task<bool> AcquireWithCounterAsync(
        string lockKey,
        string counterKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the job counter associated with a lock.
    /// </summary>
    /// <param name="counterKey">The key for the job counter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The new counter value</returns>
    Task<long> IncrementJobCounterAsync(
        string counterKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a distributed lock and resets its associated counter to 0.
    /// </summary>
    /// <param name="lockKey">The key for the distributed lock</param>
    /// <param name="counterKey">The key for the job counter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReleaseWithCounterAsync(
        string lockKey,
        string counterKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a lock is currently held.
    /// </summary>
    /// <param name="lockKey">The key for the distributed lock</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if locked, false otherwise</returns>
    Task<bool> IsLockedAsync(
        string lockKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current value of a job counter.
    /// </summary>
    /// <param name="counterKey">The key for the job counter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current counter value</returns>
    Task<long> GetJobCounterAsync(
        string counterKey,
        CancellationToken cancellationToken = default);
}

public class LockService : ILockService
{
    private readonly IRedisCacheService _redisCacheService;
    private readonly ILogger<LockService> _logger;

    public LockService(
        IRedisCacheService redisCacheService,
        ILogger<LockService> logger)
    {
        _redisCacheService = redisCacheService ?? throw new ArgumentNullException(nameof(redisCacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> AcquireWithCounterAsync(
        string lockKey,
        string counterKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Lock key cannot be null or empty", nameof(lockKey));
        if (string.IsNullOrWhiteSpace(counterKey))
            throw new ArgumentException("Counter key cannot be null or empty", nameof(counterKey));

        _logger.LogDebug("Attempting to acquire lock: {LockKey} with expiration: {Expiration}", lockKey, expiration);

        var lockAcquired = await _redisCacheService.AcquireLockAsync(lockKey, expiration, cancellationToken);

        if (!lockAcquired)
        {
            _logger.LogWarning("Failed to acquire lock: {LockKey}", lockKey);
            return false;
        }

        // Initialize counter to 0
        await _redisCacheService.SetCounterAsync(counterKey, 0, cancellationToken);
        _logger.LogInformation("Acquired lock: {LockKey}, initialized counter: {CounterKey}", lockKey, counterKey);

        return true;
    }

    public async Task<long> IncrementJobCounterAsync(
        string counterKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(counterKey))
            throw new ArgumentException("Counter key cannot be null or empty", nameof(counterKey));

        var newValue = await _redisCacheService.IncrementCounterAsync(counterKey, cancellationToken);
        _logger.LogDebug("Incremented job counter: {CounterKey} to {Value}", counterKey, newValue);
        return newValue;
    }

    public async Task ReleaseWithCounterAsync(
        string lockKey,
        string counterKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Lock key cannot be null or empty", nameof(lockKey));
        if (string.IsNullOrWhiteSpace(counterKey))
            throw new ArgumentException("Counter key cannot be null or empty", nameof(counterKey));

        _logger.LogInformation("Releasing lock: {LockKey}, resetting counter: {CounterKey}", lockKey, counterKey);

        await _redisCacheService.ReleaseLockAsync(lockKey, cancellationToken);
        await _redisCacheService.SetCounterAsync(counterKey, 0, cancellationToken);
    }

    public async Task<bool> IsLockedAsync(
        string lockKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Lock key cannot be null or empty", nameof(lockKey));

        return await _redisCacheService.IsLockedAsync(lockKey, cancellationToken);
    }

    public async Task<long> GetJobCounterAsync(
        string counterKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(counterKey))
            throw new ArgumentException("Counter key cannot be null or empty", nameof(counterKey));

        return await _redisCacheService.GetCounterAsync(counterKey, cancellationToken);
    }
}
