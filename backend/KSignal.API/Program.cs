using Kalshi.Api;
using Kalshi.Api.Configuration;
using KSignal.API.Data;
using Microsoft.EntityFrameworkCore;
using KSignal.API.Services;
using KSignal.API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Logging;
using MassTransit;
using ClickHouse.EntityFrameworkCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "KSignal API",
        Version = "v1",
        Description = "API for Kalshi Signals - Wrapper around Kalshi Trade API"
    });
});

// Prefer environment variables for all settings; fall back to configuration placeholders only
var connectionString = BuildConnectionString(builder.Configuration);
builder.Configuration["ConnectionStrings:KalshiClickHouse"] = connectionString;

builder.Services.AddDbContext<KalshiDbContext>(options =>
    options.UseClickHouse(connectionString));

// Register Kalshi API Client
// For public endpoints, we can use without authentication
// For authenticated endpoints, configure with API credentials from appsettings.json
var kalshiConfig = builder.Configuration.GetSection("KalshiApi");
var apiKey = Environment.GetEnvironmentVariable("KALSHI_API_KEY") ?? kalshiConfig["ApiKey"];
var privateKeyPath = Environment.GetEnvironmentVariable("KALSHI_PRIVATE_KEY_PATH") ?? kalshiConfig["PrivateKeyPath"];
var baseUrl = Environment.GetEnvironmentVariable("KALSHI_BASE_URL") ?? kalshiConfig["BaseUrl"];

string? privateKey = null;
if (!string.IsNullOrWhiteSpace(privateKeyPath))
{
    var fullPath = Path.Combine(builder.Environment.ContentRootPath, privateKeyPath);
    if (File.Exists(fullPath))
    {
        privateKey = File.ReadAllText(fullPath);
    }
}

// If not found via config path, try common locations
if (string.IsNullOrWhiteSpace(privateKey))
{
    // Try relative to project root (../../Market.txt from KSignal.API)
    var projectRootPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "Market.txt");
    if (File.Exists(projectRootPath))
    {
        privateKey = File.ReadAllText(projectRootPath);
    }
    else
    {
        // Try in backend folder
        var backendPath = Path.Combine(builder.Environment.ContentRootPath, "..", "Market.txt");
        if (File.Exists(backendPath))
        {
            privateKey = File.ReadAllText(backendPath);
        }
    }
}

if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(privateKey))
{
    var apiConfig = new KalshiApiConfiguration
    {
        ApiKey = apiKey,
        PrivateKey = privateKey,
        BaseUrl = baseUrl ?? throw new InvalidOperationException("Kalshi API base URL is not configured. Set KALSHI_BASE_URL.")
    };
    builder.Services.AddSingleton(new KalshiClient(apiConfig));
}
else
{
    // Use unauthenticated client for public endpoints
    builder.Services.AddSingleton(new KalshiClient());
}

var rabbitSection = builder.Configuration.GetSection("RabbitMq");
var rabbitAddress = Environment.GetEnvironmentVariable("RABBITMQ_ADDRESS") ?? rabbitSection["Address"];
var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? rabbitSection["Host"] ?? "localhost";
var rabbitPortEnv = Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? rabbitSection["Port"] ?? "5672";
var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? rabbitSection["Username"] ?? "guest";
var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? rabbitSection["Password"] ?? "guest";
var rabbitVirtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST") ?? rabbitSection["VirtualHost"] ?? "/";
var rabbitPort = ushort.TryParse(rabbitPortEnv, out var parsedPort) ? parsedPort : (ushort)5672;

if (!string.IsNullOrWhiteSpace(rabbitAddress) && Uri.TryCreate(rabbitAddress, UriKind.Absolute, out var uri))
{
    rabbitHost = string.IsNullOrWhiteSpace(uri.Host) ? rabbitHost : uri.Host;
    if (!uri.IsDefaultPort)
    {
        rabbitPort = uri.Port <= ushort.MaxValue ? (ushort)uri.Port : rabbitPort;
    }
    // Parse virtual host from URI path
    if (!string.IsNullOrWhiteSpace(uri.AbsolutePath))
    {
        var vhost = uri.AbsolutePath.TrimStart('/');
        // If path is "/" or empty after trimming, use default "/"
        rabbitVirtualHost = string.IsNullOrWhiteSpace(vhost) ? "/" : vhost;
    }

    if (!string.IsNullOrWhiteSpace(uri.UserInfo))
    {
        var parts = uri.UserInfo.Split(':', 2);
        rabbitUser = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0] : rabbitUser;
        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
        {
            rabbitPass = parts[1];
        }
    }
}

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddConsumer<KSignal.API.SynchronizeConsumers.SynchronizeMarketDataConsumer>(cfg =>
    {
        cfg.ConcurrentMessageLimit = 5;
    });
    x.AddConsumer<KSignal.API.SynchronizeConsumers.SynchronizeTagsCategoriesConsumer>();
    x.AddConsumer<KSignal.API.SynchronizeConsumers.SynchronizeSeriesConsumer>();
    x.AddConsumer<KSignal.API.SynchronizeConsumers.SynchronizeEventsConsumer>();
    x.AddConsumer<KSignal.API.SynchronizeConsumers.SynchronizeEventDetailConsumer>(cfg =>
    {
        cfg.ConcurrentMessageLimit = 3;
    });
    x.AddConsumer<KSignal.API.SynchronizeConsumers.SynchronizeOrderbookConsumer>();
    x.AddConsumer<KSignal.API.SynchronizeConsumers.SynchronizeCandlesticksConsumer>();
    x.AddConsumer<KSignal.API.SynchronizeConsumers.ProcessMarketAnalyticsConsumer>();
    x.AddConsumer<KSignal.API.SynchronizeConsumers.CleanupMarketDataConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var logger = context.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Configuring MassTransit RabbitMQ connection: Host={Host}, Port={Port}, VirtualHost={VirtualHost}, Username={Username}",
            rabbitHost, rabbitPort, rabbitVirtualHost, rabbitUser);

        cfg.Host(rabbitHost, rabbitPort, rabbitVirtualHost, h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddScoped<KalshiService>();
builder.Services.AddScoped<ChartService>();
builder.Services.AddScoped<SynchronizationService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<CleanupService>();
builder.Services.AddScoped<ISyncLogService, SyncLogService>();
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.AddScoped<StripeSubscriptionService>();
// builder.Services.AddSingleton<RefreshService>(); // Commented out - RefreshService is currently commented out

// Register RabbitMQ management service for queue administration
builder.Services.AddSingleton<RabbitMqManagementService>();

// Register Redis cache service as singleton (connection pooling)
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

// Register Lock service as singleton (depends on Redis)
builder.Services.AddSingleton<ILockService, LockService>();

// JWT authentication
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "development-placeholder-secret";
if (jwtSecret == "development-placeholder-secret")
{
    Console.WriteLine("Warning: JWT_SECRET is not set. Using development placeholder key.");
    Console.WriteLine("Warning: JWT tokens created with this key will not be valid in production.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey
    };

    // Add event handlers to log JWT validation errors
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, 
                "JWT authentication failed. Error: {Error}, Path: {Path}", 
                context.Exception?.Message, 
                context.Request.Path);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(
                "JWT challenge triggered. Error: {Error}, ErrorDescription: {ErrorDescription}, Path: {Path}",
                context.Error,
                context.ErrorDescription,
                context.Request.Path);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var firebaseId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? context.Principal?.FindFirst("sub")?.Value;
            logger.LogDebug("JWT token validated successfully for FirebaseId: {FirebaseId}", firebaseId);
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

// Log sync_event_start on backend startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var syncLogService = scope.ServiceProvider.GetRequiredService<ISyncLogService>();
        await syncLogService.LogSyncEventAsync("sync_event_start", 0);
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to log sync_event_start on backend startup");
    }
}

// Validate JWT_SECRET configuration at startup
var jwtSecretCheck = Environment.GetEnvironmentVariable("JWT_SECRET");
if (string.IsNullOrWhiteSpace(jwtSecretCheck) || jwtSecretCheck == "development-placeholder-secret")
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("JWT_SECRET is not configured or using placeholder. JWT authentication may fail.");
    if (app.Environment.IsProduction())
    {
        logger.LogError("JWT_SECRET must be set in production environment!");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Don't force HTTPS redirect in development
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

string BuildConnectionString(ConfigurationManager configuration)
{
    var dbHost = Environment.GetEnvironmentVariable("KALSHI_DB_HOST") ?? "kalshisignals.com";
    var dbUser = Environment.GetEnvironmentVariable("KALSHI_DB_USER");
    var dbPassword = Environment.GetEnvironmentVariable("KALSHI_DB_PASSWORD");
    var dbName = Environment.GetEnvironmentVariable("KALSHI_DB_NAME") ?? "kalshi_signals";
    var dbPort = Environment.GetEnvironmentVariable("KALSHI_DB_PORT") ?? "8123";
    var dbProtocol = Environment.GetEnvironmentVariable("KALSHI_DB_PROTOCOL") ?? "http";

    if (!string.IsNullOrWhiteSpace(dbUser) && !string.IsNullOrWhiteSpace(dbPassword))
    {
        // ClickHouse.Driver uses HTTP protocol on port 8123 by default
        // Format per official documentation: https://clickhouse.com/docs/integrations/csharp
        // ClickHouse.Driver expects 'Username' parameter
        return $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";
    }

    var envConnectionString = Environment.GetEnvironmentVariable("KALSHI_DB_CONNECTION");
    var connectionString = !string.IsNullOrWhiteSpace(envConnectionString)
        ? envConnectionString
        : configuration.GetConnectionString("KalshiClickHouse");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Database connection string is not configured. Set KALSHI_DB_HOST, KALSHI_DB_USER, KALSHI_DB_PASSWORD, and KALSHI_DB_NAME or KALSHI_DB_CONNECTION.");
    }

    return connectionString;
}
