using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using web_asp.Services;
using KSignals.DTO;

namespace web_asp.Pages.User;

public class AccountModel : AuthenticatedPageModel
{
    private readonly BackendClient _backendClient;
    private readonly ILogger<AccountModel> _logger;

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    public string LastName { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public AccountModel(BackendClient backendClient, ILogger<AccountModel> logger)
    {
        _backendClient = backendClient;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        var jwt = GetCookie("ksignals_jwt");
        var profileResult = string.IsNullOrWhiteSpace(jwt)
            ? (Success: false, ErrorMessage: "Authentication required. Please log in again.", Response: (UserProfileResponse?)null)
            : await _backendClient.GetUserProfileAsync(jwt);

        if (!profileResult.Success && IsAuthExpired(profileResult.ErrorMessage))
        {
            var refreshResult = await RefreshSessionAsync();
            if (refreshResult.Success && !string.IsNullOrWhiteSpace(refreshResult.Jwt))
            {
                jwt = refreshResult.Jwt;
                profileResult = await _backendClient.GetUserProfileAsync(jwt);
            }
            else
            {
                ErrorMessage = refreshResult.ErrorMessage ?? profileResult.ErrorMessage;
                return;
            }
        }

        if (!profileResult.Success || profileResult.Response == null)
        {
            ErrorMessage = profileResult.ErrorMessage ?? "Unable to load your profile.";
            if (IsAuthExpired(profileResult.ErrorMessage))
            {
                Response.Cookies.Delete("ksignals_jwt");
            }
            return;
        }

        MapProfile(profileResult.Response);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        FirstName = FirstName?.Trim() ?? string.Empty;
        LastName = LastName?.Trim() ?? string.Empty;
        Username = Request.Cookies.TryGetValue("ksignals_username", out var cookieUsername) ? cookieUsername : Username?.Trim() ?? string.Empty;
        Email = Request.Cookies.TryGetValue("ksignals_email", out var cookieEmail) ? cookieEmail : Email?.Trim() ?? string.Empty;

        var jwt = GetCookie("ksignals_jwt");
        if (string.IsNullOrWhiteSpace(jwt))
        {
            var refresh = await RefreshSessionAsync();
            if (!refresh.Success || string.IsNullOrWhiteSpace(refresh.Jwt))
            {
                ErrorMessage = refresh.ErrorMessage ?? "Authentication required. Please log in again.";
                return Page();
            }

            jwt = refresh.Jwt;
        }

        if (string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName))
        {
            ErrorMessage = "Please provide at least a first or last name.";
            return Page();
        }

        var (success, errorMessage, response) = await _backendClient.UpdateNameAsync(jwt, FirstName, LastName);

        if (!success && IsAuthExpired(errorMessage))
        {
            var refresh = await RefreshSessionAsync();
            if (refresh.Success && !string.IsNullOrWhiteSpace(refresh.Jwt))
            {
                jwt = refresh.Jwt;
                (success, errorMessage, response) = await _backendClient.UpdateNameAsync(jwt, FirstName, LastName);
            }
        }

        if (!success)
        {
            ErrorMessage = errorMessage ?? "Failed to update name. Please try again.";
            if (IsAuthExpired(errorMessage))
            {
                Response.Cookies.Delete("ksignals_jwt");
            }
            return Page();
        }

        if (response != null)
        {
            var updatedName = response.Name ?? $"{FirstName} {LastName}".Trim();
            var nameParts = updatedName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            FirstName = nameParts.Length > 0 ? nameParts[0] : FirstName;
            LastName = nameParts.Length > 1 ? nameParts[1] : LastName;
            Username = response.Username ?? Username;
            Email = response.Email ?? Email;
            jwt = response.Token ?? jwt;

            SetAuthCookies(response, GetCookie("ksignals_firebase_id"));
        }

        StatusMessage = "Name updated successfully.";

        var (profileSuccess, _, profile) = await _backendClient.GetUserProfileAsync(jwt);
        if (profileSuccess && profile != null)
        {
            MapProfile(profile);
        }

        return Page();
    }

    private void MapProfile(UserProfileResponse profile)
    {
        Username = profile.Username;
        Email = profile.Email;
        FirstName = profile.FirstName;
        LastName = profile.LastName;
        CreatedAt = profile.CreatedAt;
        UpdatedAt = profile.UpdatedAt;
    }

    private async Task<(bool Success, string? Jwt, string? ErrorMessage)> RefreshSessionAsync()
    {
        var firebaseId = GetCookie("ksignals_firebase_id");
        if (string.IsNullOrWhiteSpace(firebaseId))
        {
            return (false, null, "Authentication required. Please log in again.");
        }

        var username = GetCookie("ksignals_username");
        var email = GetCookie("ksignals_email");
        var nameCookie = GetCookie("ksignals_name");

        var nameParts = string.IsNullOrWhiteSpace(nameCookie)
            ? Array.Empty<string>()
            : nameCookie.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        var firstName = nameParts.Length > 0 ? nameParts[0] : FirstName;
        var lastName = nameParts.Length > 1 ? nameParts[1] : LastName;

        var (success, errorMessage, signIn) = await _backendClient.LoginAsync(
            firebaseId,
            username,
            firstName,
            lastName,
            email);

        if (!success || signIn == null || string.IsNullOrWhiteSpace(signIn.Token))
        {
            return (false, errorMessage ?? "Authentication required. Please log in again.", null);
        }

        SetAuthCookies(signIn, firebaseId);

        Username = signIn.Username ?? username ?? Username;
        Email = signIn.Email ?? email ?? Email;

        return (true, signIn.Token, null);
    }

    private void SetAuthCookies(SignInResponse response, string? firebaseId)
    {
        var isSecure = Request.IsHttps;
        var expires = DateTimeOffset.UtcNow.AddDays(7);
        var updatedName = response.Name ?? $"{FirstName} {LastName}".Trim();

        if (!string.IsNullOrWhiteSpace(firebaseId))
        {
            Response.Cookies.Append("ksignals_firebase_id", firebaseId, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Strict,
                Expires = expires
            });
        }

        Response.Cookies.Append("ksignals_jwt", response.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Expires = expires
        });

        Response.Cookies.Append("ksignals_username", response.Username ?? string.Empty, new CookieOptions
        {
            HttpOnly = false,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Expires = expires
        });

        Response.Cookies.Append("ksignals_name", updatedName, new CookieOptions
        {
            HttpOnly = false,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Expires = expires
        });

        Response.Cookies.Append("ksignals_email", response.Email ?? string.Empty, new CookieOptions
        {
            HttpOnly = false,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Expires = expires
        });
    }

    private string? GetCookie(string key) =>
        Request.Cookies.TryGetValue(key, out var value) ? value : null;

    private static bool IsAuthExpired(string? errorMessage) =>
        errorMessage?.Contains("sign in", StringComparison.OrdinalIgnoreCase) == true ||
        errorMessage?.Contains("expired", StringComparison.OrdinalIgnoreCase) == true;
}
