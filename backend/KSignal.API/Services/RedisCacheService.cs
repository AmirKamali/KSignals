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
            return JsonSerializer.Deserialize<T>(value!, SerializerOptions);
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

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
