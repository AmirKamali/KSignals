# RedisCache Attribute

This attribute provides declarative, aspect-oriented caching for ASP.NET Core controller actions using Redis.

## Features

- **Zero Boilerplate**: No if/else cache logic in your controllers
- **Automatic Key Generation**: Cache keys are automatically generated from routes and query parameters
- **Graceful Degradation**: If Redis is unavailable, actions execute normally
- **Configurable TTL**: Set cache duration per endpoint
- **Custom Key Prefixes**: Control cache key naming

## Usage

### Basic Usage

```csharp
[HttpGet("categories")]
[RedisCache(durationMinutes: 5)]
public async Task<IActionResult> GetCategories()
{
    var categories = await _service.GetCategoriesAsync();
    return Ok(categories);
}
```

### With Custom Key Prefix

```csharp
[HttpGet("markets")]
[RedisCache(durationMinutes: 10, cacheKeyPrefix: "markets_list")]
public async Task<IActionResult> GetMarkets([FromQuery] string? category = null)
{
    var result = await _service.GetMarketsAsync(category);
    return Ok(result);
}
```

## How It Works

The `RedisCacheAttribute` implements `IAsyncActionFilter` to intercept controller action execution:

1. **Before Action**:
   - Generates a unique cache key from the route and query parameters
   - Checks Redis for cached data
   - If found (cache hit), returns the cached response without executing the action
   - If not found (cache miss), proceeds to execute the action

2. **After Action**:
   - If the action returns an `OkObjectResult` (200 OK), caches the response
   - Sets the TTL based on `durationMinutes` parameter

## Cache Key Generation

Cache keys are generated using the following format:

```
{cacheKeyPrefix}:{routeValues}?{queryParameters}
```

### Examples

| Endpoint | Request | Cache Key |
|----------|---------|-----------|
| `/api/events/categories` | No params | `tags_by_categories` |
| `/api/markets` | `?category=World&date=this_year` | `markets?category=World&date=this_year` |
| `/api/markets` | `?category=Sports&page=2&pageSize=20` | `markets?category=Sports&page=2&pageSize=20` |

## Graceful Degradation

If Redis is unavailable or the `IRedisCacheService` is not registered:
- The attribute logs a debug message
- The action executes normally without caching
- No errors or exceptions are thrown

This ensures your application remains functional even if Redis goes down.

## Requirements

- The `IRedisCacheService` must be registered in the DI container (configured in `Program.cs`)
- Actions must return `IActionResult` or `ActionResult<T>`
- Only successful responses (`OkObjectResult`) are cached

## Configuration

The cache duration and key prefix can be configured per endpoint:

```csharp
[RedisCache(durationMinutes: 15, cacheKeyPrefix: "custom_key")]
```

### Parameters

- **durationMinutes** (int, default: 5): Cache duration in minutes
- **cacheKeyPrefix** (string?, optional): Custom prefix for cache keys. If not provided, uses `{ControllerName}:{ActionName}`

## Implementation Details

### Filter Type

The attribute uses `IAsyncActionFilter` which:
- Executes before and after the action
- Can short-circuit the pipeline (for cache hits)
- Has access to the action context and result

### Thread Safety

The Redis connection (via `IRedisCacheService`) is thread-safe and uses connection pooling, making this attribute safe for concurrent requests.

### Logging

The attribute logs the following:
- Debug: Cache key generation
- Debug: Cache misses
- Information: Cache hits
- Debug: Successful cache writes

## Best Practices

1. **Use for expensive operations**: Cache endpoints that query databases, external APIs, or perform expensive computations
2. **Set appropriate TTL**: Balance between data freshness and cache efficiency
3. **Use descriptive key prefixes**: Makes cache management easier
4. **Don't cache user-specific data**: Unless the cache key includes user identification
5. **Monitor cache hit rate**: Use Redis CLI to check cache effectiveness

## Testing

To test caching behavior:

```bash
# Start Redis
docker run -d -p 6379:6379 redis:7-alpine

# Make first request (cache miss)
curl http://localhost:3006/api/events/categories

# Make second request (cache hit - should be faster)
curl http://localhost:3006/api/events/categories

# Check cache keys
docker exec -it redis redis-cli KEYS "*"

# Check TTL
docker exec -it redis redis-cli TTL "tags_by_categories"
```

## Extending the Attribute

To add custom behavior:

1. Modify `GenerateCacheKey()` for different key generation strategies
2. Override `OnActionExecutionAsync()` to add custom logic
3. Add support for different response types beyond `OkObjectResult`

## Performance Considerations

- **Network Latency**: Redis lookups add ~1-5ms to cache hits
- **Serialization Overhead**: JSON serialization/deserialization has minimal overhead
- **Memory Usage**: Monitor Redis memory usage and set eviction policies
- **Connection Pooling**: The singleton `IRedisCacheService` ensures efficient connection reuse

## Troubleshooting

### Cache not working?

1. Verify Redis is running: `docker ps | grep redis`
2. Check Redis connection: `docker exec -it redis redis-cli ping`
3. Enable debug logging in `appsettings.json`
4. Check that `IRedisCacheService` is registered in DI

### Stale data?

1. Clear specific cache: `docker exec -it redis redis-cli DEL "key_name"`
2. Clear all cache: `docker exec -it redis redis-cli FLUSHALL`
3. Reduce TTL for frequently changing data
