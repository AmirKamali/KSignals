using ClickHouse.EntityFrameworkCore.Extensions;
using DotNetEnv;
using KSignal.API.Data;
using KSignal.API.Models;
using kadmin.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Load .env for local secrets if present
var envFileName = builder.Environment.IsDevelopment() ? ".env.dev" : ".env";
var envPath = Path.Combine(builder.Environment.ContentRootPath, envFileName);
if (!File.Exists(envPath))
{
    // Fallback to repo root if running from nested folder
    var parentPath = Path.Combine(builder.Environment.ContentRootPath, "..", envFileName);
    envPath = File.Exists(parentPath) ? parentPath : envPath;
}
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = BuildConnectionString(builder.Configuration);
builder.Configuration["ConnectionStrings:KalshiClickHouse"] = connectionString;

builder.Services.AddDbContext<KalshiDbContext>(options =>
    options.UseClickHouse(connectionString, clickhouse =>
        clickhouse.MaxBatchSize(500)));

var stripeSection = builder.Configuration.GetSection(StripeOptions.SectionName);
builder.Services.Configure<StripeOptions>(options =>
{
    options.SecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? stripeSection["SecretKey"] ?? string.Empty;
    options.PublishableKey = Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY") ?? stripeSection["PublishableKey"] ?? string.Empty;
    options.WebhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? stripeSection["WebhookSecret"] ?? string.Empty;
    options.SuccessUrl = Environment.GetEnvironmentVariable("STRIPE_SUCCESS_URL") ?? stripeSection["SuccessUrl"] ?? string.Empty;
    options.CancelUrl = Environment.GetEnvironmentVariable("STRIPE_CANCEL_URL") ?? stripeSection["CancelUrl"] ?? string.Empty;
    options.BillingPortalReturnUrl = Environment.GetEnvironmentVariable("STRIPE_BILLING_PORTAL_RETURN_URL") ?? stripeSection["BillingPortalReturnUrl"] ?? string.Empty;
    options.CoreDataPriceId = Environment.GetEnvironmentVariable("STRIPE_CORE_DATA_PRICE_ID") ?? stripeSection["CoreDataPriceId"] ?? string.Empty;
    options.CoreDataAnnualPriceId = Environment.GetEnvironmentVariable("STRIPE_CORE_DATA_ANNUAL_PRICE_ID") ?? stripeSection["CoreDataAnnualPriceId"];
    options.PremiumPriceId = Environment.GetEnvironmentVariable("STRIPE_PREMIUM_PRICE_ID") ?? stripeSection["PremiumPriceId"];
});

builder.Services.AddScoped<AdminDataService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

static string BuildConnectionString(ConfigurationManager configuration)
{
    var dbHost = Environment.GetEnvironmentVariable("KALSHI_DB_HOST") ?? "kalshisignals.com";
    var dbUser = Environment.GetEnvironmentVariable("KALSHI_DB_USER");
    var dbPassword = Environment.GetEnvironmentVariable("KALSHI_DB_PASSWORD");
    var dbName = Environment.GetEnvironmentVariable("KALSHI_DB_NAME") ?? "kalshi_signals";
    var dbPort = Environment.GetEnvironmentVariable("KALSHI_DB_PORT") ?? "8123";

    if (!string.IsNullOrWhiteSpace(dbUser) && !string.IsNullOrWhiteSpace(dbPassword))
    {
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
