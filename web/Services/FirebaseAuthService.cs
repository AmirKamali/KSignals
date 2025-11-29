using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using web_asp.Models;

namespace web_asp.Services;

public interface IFirebaseAuthService
{
    Task<FirebaseUserInfo?> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}

public class FirebaseAuthService : IFirebaseAuthService
{
    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebaseAuthService> _logger;
    private readonly object _initLock = new();

    public FirebaseAuthService(IOptions<FirebaseOptions> options, ILogger<FirebaseAuthService> logger)
    {
        _options = options.Value;
        _logger = logger;

        EnsureInitialized();
    }

    public async Task<FirebaseUserInfo?> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        try
        {
            var decoded = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken, cancellationToken);

            var displayName = decoded.Claims.TryGetValue("name", out var nameClaim)
                ? nameClaim?.ToString()
                : null;
            var email = decoded.Claims.TryGetValue("email", out var emailClaim)
                ? emailClaim?.ToString()
                : null;
            var firstName = decoded.Claims.TryGetValue("given_name", out var givenName)
                ? givenName?.ToString()
                : null;
            var lastName = decoded.Claims.TryGetValue("family_name", out var familyName)
                ? familyName?.ToString()
                : null;

            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
            {
                (firstName, lastName) = SplitName(displayName);
            }

            return new FirebaseUserInfo(
                decoded.Uid,
                email,
                displayName,
                firstName,
                lastName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify Firebase ID token");
            return null;
        }
    }

    private void EnsureInitialized()
    {
        if (FirebaseApp.DefaultInstance != null)
        {
            return;
        }

        lock (_initLock)
        {
            if (FirebaseApp.DefaultInstance != null)
            {
                return;
            }

            var credential = BuildCredential();
            var appOptions = new AppOptions
            {
                Credential = credential
            };

            if (!string.IsNullOrWhiteSpace(_options.ProjectId))
            {
                appOptions.ProjectId = _options.ProjectId;
            }

            FirebaseApp.Create(appOptions);
            _logger.LogInformation("Firebase Admin initialized for project {ProjectId}", appOptions.ProjectId ?? "(default)");
        }
    }

    private GoogleCredential BuildCredential()
    {
        // Prefer file-based credentials
        var pathCandidates = new[]
        {
            _options.CredentialPath,
            Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_FILE"),
        };

        foreach (var path in pathCandidates)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            _logger.LogInformation("Initializing Firebase using service account file at {Path}", path);
            return GoogleCredential.FromFile(path);
        }

        // Fall back to inline JSON (useful in containerized deployments)
        var jsonCandidate = _options.CredentialJson
            ?? Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_BASE64");

        if (!string.IsNullOrWhiteSpace(jsonCandidate))
        {
            var json = DecodeIfBase64(jsonCandidate);
            _logger.LogInformation("Initializing Firebase using inline credentials");
            return GoogleCredential.FromJson(json);
        }

        _logger.LogWarning("Firebase credentials not configured; attempting to use application default credentials.");
        return GoogleCredential.GetApplicationDefault();
    }

    private static string DecodeIfBase64(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            var decoded = System.Text.Encoding.UTF8.GetString(bytes);
            if (decoded.TrimStart().StartsWith("{"))
            {
                return decoded;
            }
        }
        catch
        {
            // Not base64 - treat as plain JSON
        }

        return value;
    }

    private static (string? First, string? Last) SplitName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return (null, null);
        }

        var parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var first = parts.ElementAtOrDefault(0);
        var last = parts.ElementAtOrDefault(1);
        return (first, last);
    }
}
