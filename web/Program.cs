using web_asp.Services;
using web_asp.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.Configure<BackendOptions>(options =>
{
    // Prefer env var to match the Next.js app default, fall back to appsettings.
    var baseUrl =
        builder.Configuration["BACKEND_API_BASE_URL"]
        ?? builder.Configuration.GetSection("Backend")["BaseUrl"]
        ?? "http://localhost:3006";

    options.BaseUrl = baseUrl;
    options.PublicBaseUrl =
        builder.Configuration["BACKEND_API_PUBLIC_URL"]
        ?? builder.Configuration.GetSection("Backend")["PublicBaseUrl"]
        ?? baseUrl;
});

builder.Services.AddHttpClient<BackendClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Allow authentication popups (e.g., Google/Firebase) to close themselves.
app.Use(async (context, next) =>
{
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
    await next();
});

app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
