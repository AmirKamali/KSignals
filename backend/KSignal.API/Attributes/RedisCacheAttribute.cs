using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using KSignal.API.Services;
using System.Text;

namespace KSignal.API.Attributes;

/// <summary>
/// Attribute to enable Redis caching for controller actions.
/// If Redis is unavailable, the action executes normally without caching.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RedisCacheAttribute : Attribute, IAsyncActionFilter
{
    private readonly int _durationMinutes;
    private readonly string? _cacheKeyPrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheAttribute"/> class.
    /// </summary>
    /// <param name="durationMinutes">Cache duration in minutes. Default is 5 minutes.</param>
    /// <param name="cacheKeyPrefix">Optional prefix for the cache key. If not provided, uses the action name.</param>
    public RedisCacheAttribute(int durationMinutes = 5, string? cacheKeyPrefix = null)
    {
        _durationMinutes = durationMinutes;
        _cacheKeyPrefix = cacheKeyPrefix;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cacheService = context.HttpContext.RequestServices.GetService<IRedisCacheService>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<RedisCacheAttribute>>();

        // If cache service is not available, execute action normally
        if (cacheService == null)
        {
            logger?.LogDebug("Redis cache service not available, executing action without cache");
            await next();
            return;
        }

        // Generate cache key from route and query parameters
        var cacheKey = GenerateCacheKey(context);
        logger?.LogDebug("Generated cache key: {CacheKey}", cacheKey);

        // Try to get from cache
        var cachedResult = await cacheService.GetAsync<object>(cacheKey);
        if (cachedResult != null)
        {
            logger?.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
            context.Result = new OkObjectResult(cachedResult);
            return;
        }

        logger?.LogDebug("Cache miss for key: {CacheKey}", cacheKey);

        // Execute the action
        var executedContext = await next();

        // Cache the result if successful
        if (executedContext.Result is OkObjectResult okResult && okResult.Value != null)
        {
            var expiration = TimeSpan.FromMinutes(_durationMinutes);
            await cacheService.SetAsync(cacheKey, okResult.Value, expiration);
            logger?.LogDebug("Cached result for key: {CacheKey} with expiration: {Expiration}", cacheKey, expiration);
        }
    }

    private string GenerateCacheKey(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;
        var keyBuilder = new StringBuilder();

        // Base prefix + normalized path keeps keys unique per endpoint
        var prefix = _cacheKeyPrefix ?? $"{context.Controller.GetType().Name}:{context.ActionDescriptor.DisplayName}";
        keyBuilder.Append(prefix);

        if (request.Path.HasValue)
        {
            keyBuilder.Append($":{request.Path.Value?.TrimEnd('/')}");
        }

        // Add route values (excluding controller/action) for completeness
        foreach (var routeValue in context.RouteData.Values.OrderBy(x => x.Key))
        {
            if (routeValue.Key != "controller" && routeValue.Key != "action")
            {
                keyBuilder.Append($":{routeValue.Key}={routeValue.Value}");
            }
        }

        // Add normalized query parameters to avoid collisions between filtered requests
        var queryParams = request.Query
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value.ToString())}")
            .ToList();

        if (queryParams.Count > 0)
        {
            keyBuilder.Append("?");
            keyBuilder.Append(string.Join("&", queryParams));
        }

        return keyBuilder.ToString();
    }
}
