using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using web_asp.Models;
using web_asp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.Configure<FirebaseOptions>(options =>
{
    // Base settings from configuration section
    var firebaseSection = builder.Configuration.GetSection("Firebase");
    firebaseSection.Bind(options);

    // Allow explicit environment variables to override config values
    options.ProjectId =
        builder.Configuration["FIREBASE_PROJECT_ID"]
        ?? options.ProjectId;

    options.CredentialPath =
        builder.Configuration["FIREBASE_CREDENTIALS_FILE"]
        ?? options.CredentialPath;

    options.CredentialJson =
        builder.Configuration["FIREBASE_CREDENTIALS_BASE64"]
        ?? options.CredentialJson;

    options.CookieDomain =
        builder.Configuration["FIREBASE_COOKIE_DOMAIN"]
        ?? options.CookieDomain
        ?? "KalshiSignals.com";
});
builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();

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

    Console.WriteLine($"[Config] BaseUrl: {options.BaseUrl}");
    Console.WriteLine($"[Config] PublicBaseUrl: {options.PublicBaseUrl}");
    Console.WriteLine($"[Config] Env BACKEND_API_PUBLIC_URL: {builder.Configuration["BACKEND_API_PUBLIC_URL"]}");
    Console.WriteLine($"[Config] Config Backend:PublicBaseUrl: {builder.Configuration.GetSection("Backend")["PublicBaseUrl"]}");
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

app.MapPost("/auth/firebase/login", async (
    [FromBody] FirebaseLoginRequest request,
    HttpContext context,
    IFirebaseAuthService firebaseAuth,
    BackendClient backendClient,
    IOptions<FirebaseOptions> firebaseOptions,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.IdToken))
    {
        return Results.BadRequest(new { error = "idToken is required" });
    }

    var firebaseUser = await firebaseAuth.VerifyIdTokenAsync(request.IdToken, context.RequestAborted);
    if (firebaseUser == null)
    {
        return Results.Unauthorized();
    }

    var (success, errorMessage, signIn) = await backendClient.LoginAsync(
        firebaseUser.FirebaseId,
        username: firebaseUser.DisplayName ?? firebaseUser.Email ?? firebaseUser.FirebaseId,
        firstName: firebaseUser.FirstName,
        lastName: firebaseUser.LastName,
        email: firebaseUser.Email);

    if (!success || signIn?.Token == null)
    {
        logger.LogWarning("Failed to login to backend for Firebase user {FirebaseId}: {Error}", firebaseUser.FirebaseId, errorMessage);
        return Results.BadRequest(new { error = errorMessage ?? "Unable to start session" });
    }

    var cookieDomain = ResolveCookieDomain(context.Request.Host.Host, firebaseOptions.Value.CookieDomain);
    var expires = DateTimeOffset.UtcNow.AddDays(7);
    var secure = context.Request.IsHttps || (!string.IsNullOrWhiteSpace(cookieDomain) && !IsLocalHost(context.Request.Host.Host));

    void SetCookie(string key, string? value, bool httpOnly)
    {
        context.Response.Cookies.Append(key, value ?? string.Empty, new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Domain = cookieDomain
        });
    }

    SetCookie("ksignals_jwt", signIn.Token, httpOnly: true);
    SetCookie("ksignals_firebase_id", firebaseUser.FirebaseId, httpOnly: true);
    SetCookie("ksignals_username", signIn.Username ?? firebaseUser.Email ?? firebaseUser.FirebaseId, httpOnly: false);
    SetCookie("ksignals_name", signIn.Name ?? firebaseUser.DisplayName ?? string.Empty, httpOnly: false);
    SetCookie("ksignals_email", signIn.Email ?? firebaseUser.Email ?? string.Empty, httpOnly: false);

    var needsUsername = string.IsNullOrWhiteSpace(signIn.Username)
        || signIn.Username.Contains('@')
        || signIn.Username.Equals(firebaseUser.FirebaseId, StringComparison.OrdinalIgnoreCase)
        || signIn.Username.Equals("null", StringComparison.OrdinalIgnoreCase);

    var redirectUrl = needsUsername
        ? "/user/username"
        : NormalizeReturnUrl(request.ReturnUrl);

    return Results.Ok(new
    {
        success = true,
        redirectUrl,
        needsUsername,
        username = signIn.Username
    });
});

app.MapPost("/auth/logout", (HttpContext context, IOptions<FirebaseOptions> firebaseOptions) =>
{
    var cookieDomain = ResolveCookieDomain(context.Request.Host.Host, firebaseOptions.Value.CookieDomain);
    var secure = context.Request.IsHttps || (!string.IsNullOrWhiteSpace(cookieDomain) && !IsLocalHost(context.Request.Host.Host));

    void ClearCookie(string key, bool httpOnly)
    {
        context.Response.Cookies.Append(key, string.Empty, new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            Domain = cookieDomain
        });
    }

    ClearCookie("ksignals_jwt", httpOnly: true);
    ClearCookie("ksignals_firebase_id", httpOnly: true);
    ClearCookie("ksignals_username", httpOnly: false);
    ClearCookie("ksignals_name", httpOnly: false);
    ClearCookie("ksignals_email", httpOnly: false);

    return Results.Ok(new { success = true });
});

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static bool IsLocalHost(string host) =>
    host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
    || host.Equals("127.0.0.1")
    || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

static string? ResolveCookieDomain(string host, string? configuredDomain)
{
    if (IsLocalHost(host))
    {
        return null;
    }

    if (string.IsNullOrWhiteSpace(configuredDomain))
    {
        return null;
    }

    return configuredDomain;
}

static string NormalizeReturnUrl(string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl))
    {
        return "/";
    }

    return Uri.TryCreate(returnUrl, UriKind.Relative, out _)
        ? returnUrl
        : "/";
}

internal record FirebaseLoginRequest(string IdToken, string? ReturnUrl);
