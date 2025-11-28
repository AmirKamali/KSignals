using web_asp.Services;
using web_asp.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.Configure<BackendOptions>(options =>
{
    // Prefer env var to match the Next.js app default, fall back to appsettings.
    options.BaseUrl =
        builder.Configuration["BACKEND_API_BASE_URL"]
        ?? builder.Configuration.GetSection("Backend")["BaseUrl"]
        ?? "http://localhost:3006";
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

app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapPost("/logout", (HttpContext context) =>
{
    var secure = context.Request.IsHttps;
    var expired = DateTimeOffset.UnixEpoch;
    var cookieNames = new[]
    {
        "ksignals_jwt",
        "ksignals_firebase_id",
        "ksignals_username",
        "ksignals_name",
        "ksignals_email"
    };

    foreach (var name in cookieNames)
    {
        context.Response.Cookies.Append(name, string.Empty, new CookieOptions
        {
            Expires = expired,
            Path = "/",
            HttpOnly = name is "ksignals_jwt" or "ksignals_firebase_id",
            Secure = secure,
            SameSite = SameSiteMode.Strict
        });
    }

    return Results.Ok();
});
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
