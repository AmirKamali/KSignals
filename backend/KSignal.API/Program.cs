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

var connectionString = builder.Configuration.GetConnectionString("KalshiMySql");
builder.Services.AddDbContext<KalshiDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Register Kalshi API Client
// For public endpoints, we can use without authentication
// For authenticated endpoints, configure with API credentials from appsettings.json
var kalshiConfig = builder.Configuration.GetSection("KalshiApi");
var apiKey = kalshiConfig["ApiKey"];
var privateKeyPath = kalshiConfig["PrivateKeyPath"];

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
        BaseUrl = kalshiConfig["BaseUrl"] ?? "https://api.elections.kalshi.com/trade-api/v2"
    };
    builder.Services.AddSingleton(new KalshiClient(apiConfig));
}
else
{
    // Use unauthenticated client for public endpoints
    builder.Services.AddSingleton(new KalshiClient());
}

builder.Services.AddScoped<KalshiService>();

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
