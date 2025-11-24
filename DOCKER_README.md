# Docker Setup for Kalshi Signals

This project includes a complete Docker Compose setup with Redis caching support.

## Services

The docker-compose configuration includes three services:

1. **Redis** - Cache server (Port 6379)
2. **Backend** - .NET 8 API (Port 3006)
3. **Web** - Next.js frontend (Port 3000)

## Features

### Redis Caching
- **Implementation**: Attribute-based caching using `[RedisCache]` attribute (Aspect-Oriented Programming)
- **Cache Duration**: 5 minutes (configurable per endpoint)
- **Cached Endpoints**:
  - `/api/events/categories` - Tags organized by series categories
  - `/api/markets` - Market data with filters (category, tag, date)
- **Graceful Degradation**: If Redis is unavailable, the application continues to work normally without caching
- **Auto-generated Keys**: Cache keys are automatically generated from route and query parameters
- **No Code Pollution**: Controllers contain no if/else cache logic - everything is handled by the attribute

### Configuration

Backend Redis configuration can be set via:
1. **appsettings.json**: `Redis.ConnectionString`
2. **Environment Variable**: `Redis__ConnectionString` (overrides appsettings)

Default connection: `localhost:6379` (local) or `redis:6379` (Docker)

## Quick Start

### Prerequisites
- Docker Desktop installed and running
- Docker Compose v3.8 or higher

### Running with Docker Compose

```bash
# From the project root directory
docker-compose up --build
```

This will:
1. Start Redis on port 6379
2. Build and start the backend API on port 3006
3. Build and start the web frontend on port 3000

### Accessing the Application

- **Frontend**: http://localhost:3000
- **Backend API**: http://localhost:3006
- **Swagger/API Docs**: http://localhost:3006/swagger
- **Redis**: localhost:6379 (accessible from host machine)

### Stopping the Services

```bash
# Stop all services
docker-compose down

# Stop and remove volumes (clears Redis cache)
docker-compose down -v
```

## Local Development (Without Docker)

### Running Redis Locally

If you want to run the backend locally but use Redis for caching:

```bash
# Start Redis in Docker
docker run -d -p 6379:6379 --name redis redis:7-alpine

# Run the backend
cd backend/KSignal.API
dotnet run
```

### Running Without Redis

The application will work without Redis. If Redis is not available:
- The cache service will log a warning
- All requests will bypass the cache
- No errors or crashes will occur

## Environment Variables

### Backend

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_URLS` | `http://+:3006` | Backend API URL |
| `Redis__ConnectionString` | `localhost:6379` | Redis connection string |
| `ConnectionStrings__KalshiMySql` | (from appsettings) | MySQL database connection |

### Frontend

| Variable | Default | Description |
|----------|---------|-------------|
| `BACKEND_API_BASE_URL` | `http://localhost:3006` | Backend API URL |
| `NODE_ENV` | `production` | Node environment |

## Troubleshooting

### Redis Connection Issues

If the backend can't connect to Redis:
1. Check Redis is running: `docker ps | grep redis`
2. Test Redis connection: `docker exec -it kalshi-signals-redis redis-cli ping`
3. Check backend logs: `docker logs kalshi-signals-backend`

The application will continue to work without Redis, just without caching.

### Backend Build Issues

```bash
# Rebuild backend only
docker-compose build backend

# View backend logs
docker logs -f kalshi-signals-backend
```

### Frontend Build Issues

```bash
# Rebuild frontend only
docker-compose build web

# View frontend logs
docker logs -f kalshi-signals-web
```

## Cache Management

### Viewing Cache Keys

```bash
# Connect to Redis CLI
docker exec -it kalshi-signals-redis redis-cli

# List all keys
KEYS *

# Get a specific key
GET tags_by_categories

# Check TTL (time to live)
TTL tags_by_categories
```

### Clearing Cache

```bash
# Clear all cache
docker exec -it kalshi-signals-redis redis-cli FLUSHALL

# Clear specific key
docker exec -it kalshi-signals-redis redis-cli DEL tags_by_categories
```

## Using the RedisCache Attribute

The backend uses an attribute-based caching approach for clean, maintainable code. To add caching to any controller action:

```csharp
[HttpGet("your-endpoint")]
[RedisCache(durationMinutes: 5, cacheKeyPrefix: "your_cache_key")]
public async Task<IActionResult> YourAction()
{
    // Your business logic here - no cache code needed!
    var data = await _service.GetDataAsync();
    return Ok(data);
}
```

### Attribute Parameters

- `durationMinutes` (default: 5): How long to cache the response
- `cacheKeyPrefix` (optional): Custom prefix for the cache key. If not provided, uses the controller and action name

### How It Works

1. The attribute intercepts the action execution
2. Generates a cache key from the prefix, route values, and query parameters
3. Checks Redis for cached data
4. Returns cached data if found (cache hit)
5. Executes the action if not found (cache miss)
6. Caches the result for future requests
7. If Redis is unavailable, the action executes normally without caching

### Example Cache Keys

- `/api/events/categories` → `tags_by_categories`
- `/api/markets?category=World&date=this_year` → `markets?category=World&date=this_year`

No manual cache management needed - the attribute handles everything!

## Production Deployment

For production, consider:

1. **Environment Variables**: Use secrets management for sensitive data
2. **Redis Persistence**: The current setup uses AOF (Append Only File) for persistence
3. **Health Checks**: All services include health checks for monitoring
4. **Volumes**: Redis data is persisted in a named volume
5. **Restart Policy**: Services are configured with `restart: unless-stopped`

## Architecture

```
┌─────────────┐
│   Browser   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  Web (Next) │ :3000
└──────┬──────┘
       │
       ▼
┌─────────────┐     ┌─────────────┐
│  Backend    │────▶│    Redis    │
│  (.NET 8)   │     │   (Cache)   │
└─────────────┘     └─────────────┘
     :3006               :6379
       │
       ▼
┌─────────────┐
│   MySQL DB  │
│  (External) │
└─────────────┘
```

## Notes

- The backend includes the `Market.txt` file which should be placed in the backend root directory for Kalshi API authentication
- Database connection string is configured for the external MySQL server
- Redis cache expires after 5 minutes to ensure data freshness
- All services are connected via a custom bridge network (`kalshi-network`)
