using Kalshi.Api;
using Kalshi.Api.Configuration;
using KSignal.API.Data;
using Microsoft.EntityFrameworkCore;
using KSignal.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
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
var envConnectionString = Environment.GetEnvironmentVariable("KALSHI_DB_CONNECTION");
var connectionString = !string.IsNullOrWhiteSpace(envConnectionString)
    ? envConnectionString
    : builder.Configuration.GetConnectionString("KalshiMySql");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Database connection string is not configured. Set KALSHI_DB_CONNECTION.");
}

var dbVersionString = Environment.GetEnvironmentVariable("KALSHI_DB_VERSION");
MySqlServerVersion? dbServerVersion = null;

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

// Register Redis cache service as singleton (connection pooling)
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // Don't force HTTPS redirect in development
}

app.UseAuthorization();
app.MapControllers();

app.Urls.Add("http://localhost:3006");
app.Run();
