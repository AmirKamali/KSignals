using Kalshi.Api;
using Kalshi.Api.Configuration;
using KSignal.API.Data;
using Microsoft.EntityFrameworkCore;
using KSignal.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Logging;

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
builder.Configuration["ConnectionStrings:KalshiMySql"] = connectionString;

var dbVersionString = Environment.GetEnvironmentVariable("KALSHI_DB_VERSION");
ServerVersion? dbServerVersion = null;

if (!string.IsNullOrWhiteSpace(dbVersionString) && Version.TryParse(dbVersionString, out var parsedVersion))
{
    dbServerVersion = new MySqlServerVersion(parsedVersion);
}
else
{
    try
    {
        dbServerVersion = ServerVersion.AutoDetect(connectionString);
    }
    catch
    {
        // Default to a recent MySQL 8 version when auto-detect fails (e.g., DB unreachable at startup)
        dbServerVersion = new MySqlServerVersion(new Version(8, 0, 32));
    }
}

builder.Services.AddDbContext<KalshiDbContext>(options =>
    options.UseMySql(connectionString, dbServerVersion));

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

builder.Services.AddScoped<KalshiService>();
builder.Services.AddSingleton<RefreshService>();

// Register Redis cache service as singleton (connection pooling)
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

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
    var dbHost = Environment.GetEnvironmentVariable("KALSHI_DB_HOST");
    var dbUser = Environment.GetEnvironmentVariable("KALSHI_DB_USER");
    var dbPassword = Environment.GetEnvironmentVariable("KALSHI_DB_PASSWORD");
    var dbName = Environment.GetEnvironmentVariable("KALSHI_DB_NAME");
    var dbSslMode = Environment.GetEnvironmentVariable("KALSHI_DB_SSL_MODE");

    if (!string.IsNullOrWhiteSpace(dbHost)
        && !string.IsNullOrWhiteSpace(dbUser)
        && !string.IsNullOrWhiteSpace(dbPassword)
        && !string.IsNullOrWhiteSpace(dbName))
    {
        var sslModeSegment = string.IsNullOrWhiteSpace(dbSslMode) ? "SslMode=Disabled" : $"SslMode={dbSslMode}";
        return $"Server={dbHost};Database={dbName};User ID={dbUser};Password={dbPassword};{sslModeSegment}";
    }

    var envConnectionString = Environment.GetEnvironmentVariable("KALSHI_DB_CONNECTION");
    var connectionString = !string.IsNullOrWhiteSpace(envConnectionString)
        ? envConnectionString
        : configuration.GetConnectionString("KalshiMySql");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Database connection string is not configured. Set KALSHI_DB_HOST, KALSHI_DB_USER, KALSHI_DB_PASSWORD, and KALSHI_DB_NAME or KALSHI_DB_CONNECTION.");
    }

    return connectionString;
}
