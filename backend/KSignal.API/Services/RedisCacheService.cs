using StackExchange.Redis;
using System.Text.Json;

namespace KSignal.API.Services;

public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync();
    Task DeleteByPatternAsync(string pattern, CancellationToken cancellationToken = default);
    Task FlushAllAsync(CancellationToken cancellationToken = default);

    // Distributed lock methods
    Task<bool> AcquireLockAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task<bool> ReleaseLockAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> IsLockedAsync(string key, CancellationToken cancellationToken = default);

    // Counter methods for job tracking
    Task<long> IncrementCounterAsync(string key, CancellationToken cancellationToken = default);
    Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default);
    Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> SetCounterAsync(string key, long value, CancellationToken cancellationToken = default);
}

public class RedisCacheService : IRedisCacheService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<RedisCacheService> _logger;
    private readonly string? _connectionString;
    private ConnectionMultiplexer? _redis;
    private IDatabase? _db;
    private bool _isAvailable;

    public RedisCacheService(IConfiguration configuration, ILogger<RedisCacheService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = Environment.GetEnvironmentVariable("KALSHI_REDIS_CONNECTION")
            ?? configuration.GetValue<string>("Redis:ConnectionString");

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogWarning("Redis connection string is not configured. Cache will be disabled.");
            _isAvailable = false;
            return;
        }

        try
        {
            _logger.LogInformation("Connecting to Redis at {ConnectionString}", _connectionString);
            _redis = ConnectionMultiplexer.Connect(_connectionString);
            _db = _redis.GetDatabase();
            _isAvailable = true;
            _logger.LogInformation("Successfully connected to Redis");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Redis. Cache will be disabled. Error: {Message}", ex.Message);
            _isAvailable = false;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        if (!_isAvailable || _redis == null || !_redis.IsConnected)
        {
            return false;
        }

        try
        {
            // Ping to verify connection is still alive
            var endpoints = _redis.GetEndPoints();
            if (endpoints.Length == 0) return false;

            var server = _redis.GetServer(endpoints[0]);
            await server.PingAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis ping failed. Cache will be disabled temporarily.");
            return false;
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _db == null)
        {
            return default;
        }

        try
        {
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return default;
            }

            _logger.LogDebug("Cache hit for key: {Key}", key);
            // For .NET 10 the ReadOnlySpan<byte> and string overloads are ambiguous; use explicit byte[] conversion.
            return JsonSerializer.Deserialize<T>((byte[])value!, SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get value from Redis for key: {Key}. Error: {Message}", key, ex.Message);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _db == null)
        {
            return;
        }

        try
        {
            var serialized = JsonSerializer.Serialize(value, SerializerOptions);
            await _db.StringSetAsync(key, serialized, expiration);
            _logger.LogDebug("Successfully cached value for key: {Key} with expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set value in Redis for key: {Key}. Error: {Message}", key, ex.Message);
        }
    }

    public async Task DeleteByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _redis == null || _db == null)
        {
            _logger.LogDebug("Redis not available, skipping delete by pattern: {Pattern}", pattern);
            return;
        }

        try
        {
            var endpoints = _redis.GetEndPoints();
            if (endpoints.Length == 0)
            {
                _logger.LogWarning("No Redis endpoints available for delete by pattern: {Pattern}", pattern);
                return;
            }

            var server = _redis.GetServer(endpoints[0]);
            var keys = server.Keys(pattern: pattern);
            var deletedCount = 0;

            foreach (var key in keys)
            {
                await _db.KeyDeleteAsync(key);
                deletedCount++;
            }

            _logger.LogInformation("Deleted {Count} keys matching pattern: {Pattern}", deletedCount, pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete keys by pattern: {Pattern}. Error: {Message}", pattern, ex.Message);
        }
    }

    public async Task FlushAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _redis == null)
        {
            _logger.LogDebug("Redis not available, skipping flush");
            return;
        }

        try
        {
            var endpoints = _redis.GetEndPoints();
            if (endpoints.Length == 0)
            {
                _logger.LogWarning("No Redis endpoints available for flush");
                return;
            }

            var server = _redis.GetServer(endpoints[0]);
            await server.FlushDatabaseAsync();
            _logger.LogInformation("Successfully flushed all Redis keys");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flush Redis database. Error: {Message}", ex.Message);
        }
    }

    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _db == null)
        {
            _logger.LogWarning("Redis not available, cannot acquire lock for key: {Key}", key);
            return false;
        }

        try
        {
            // Use SET with NX (only set if not exists) and EX (expiration) flags
            var lockAcquired = await _db.StringSetAsync(key, "locked", expiration, When.NotExists);

            if (lockAcquired)
            {
                _logger.LogInformation("Successfully acquired lock for key: {Key} with expiration: {Expiration}", key, expiration);
            }
            else
            {
                _logger.LogDebug("Failed to acquire lock for key: {Key} (already locked)", key);
            }

            return lockAcquired;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acquire lock for key: {Key}. Error: {Message}", key, ex.Message);
            return false;
        }
    }

    public async Task<bool> ReleaseLockAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _db == null)
        {
            _logger.LogWarning("Redis not available, cannot release lock for key: {Key}", key);
            return false;
        }

        try
        {
            var deleted = await _db.KeyDeleteAsync(key);

            if (deleted)
            {
                _logger.LogInformation("Successfully released lock for key: {Key}", key);
            }
            else
            {
                _logger.LogDebug("Lock key {Key} was not found or already expired", key);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release lock for key: {Key}. Error: {Message}", key, ex.Message);
            return false;
        }
    }

    public async Task<bool> IsLockedAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _db == null)
        {
            return false;
        }

        try
        {
            return await _db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check lock status for key: {Key}. Error: {Message}", key, ex.Message);
            return false;
        }
    }

    public async Task<long> IncrementCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _db == null)
        {
            _logger.LogWarning("Redis not available, cannot increment counter for key: {Key}", key);
            return 0;
        }

        try
        {
            var newValue = await _db.StringIncrementAsync(key);
            _logger.LogDebug("Incremented counter {Key} to {Value}", key, newValue);
            return newValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to increment counter for key: {Key}. Error: {Message}", key, ex.Message);
            return 0;
        }
    }

    public async Task<long> DecrementCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _db == null)
        {
            _logger.LogWarning("Redis not available, cannot decrement counter for key: {Key}", key);
            return 0;
        }

        try
        {
            var newValue = await _db.StringDecrementAsync(key);
            _logger.LogDebug("Decremented counter {Key} to {Value}", key, newValue);
            return newValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrement counter for key: {Key}. Error: {Message}", key, ex.Message);
            return 0;
        }
    }

    public async Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _db == null)
        {
            return 0;
        }

        try
        {
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue)
            {
                return 0;
            }

            // Explicitly cast to string to avoid ambiguity in .NET 10
            return long.TryParse((string?)value, out var counter) ? counter : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get counter for key: {Key}. Error: {Message}", key, ex.Message);
            return 0;
        }
    }

    public async Task<bool> SetCounterAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        if (!_isAvailable || _db == null)
        {
            _logger.LogWarning("Redis not available, cannot set counter for key: {Key}", key);
            return false;
        }

        try
        {
            await _db.StringSetAsync(key, value);
            _logger.LogDebug("Set counter {Key} to {Value}", key, value);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set counter for key: {Key}. Error: {Message}", key, ex.Message);
            return false;
        }
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
