using Microsoft.AspNetCore.Mvc;
using web_asp.Services;

namespace web_asp.Pages.User;

public class UsernameModel : AuthenticatedPageModel
{
    private readonly BackendClient _backendClient;
    private readonly ILogger<UsernameModel> _logger;

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string FirebaseId { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public UsernameModel(BackendClient backendClient, ILogger<UsernameModel> logger)
    {
        _backendClient = backendClient;
        _logger = logger;
    }

    public void OnGet()
    {
        // Get Firebase ID from parent class method if needed
        FirebaseId = GetFirebaseIdFromCookies() ?? string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(FirebaseId))
        {
            ErrorMessage = "Authentication required. Please log in again.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Username is required";
            return Page();
        }

        Username = Username.Trim();

        // Validate username format
        if (Username.Length < 3)
        {
            ErrorMessage = "Username must be at least 3 characters";
            return Page();
        }

        if (Username.Length > 30)
        {
            ErrorMessage = "Username must be less than 30 characters";
            return Page();
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(Username, @"^[a-zA-Z0-9_]+$"))
        {
            ErrorMessage = "Username can only contain letters, numbers, and underscores";
            return Page();
        }

        // Call backend API
        var (success, errorMessage, response) = await _backendClient.SetUsernameAsync(FirebaseId, Username);

        if (!success)
        {
            ErrorMessage = errorMessage ?? "Failed to set username. Please try again.";
            return Page();
        }

        if (response != null)
        {
            // Only use Secure flag in production (HTTPS)
            var isSecure = Request.IsHttps;
            var expires = DateTimeOffset.UtcNow.AddDays(7);

            // Store the token in a cookie
            Response.Cookies.Append("ksignals_jwt", response.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Strict,
                Expires = expires
            });

            // Store Firebase ID for authentication check
            Response.Cookies.Append("ksignals_firebase_id", FirebaseId, new CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = SameSiteMode.Strict,
                Expires = expires
            });

            // Store user info in cookies for client-side access
            Response.Cookies.Append("ksignals_username", response.Username, new CookieOptions
            {
                HttpOnly = false,
                Secure = isSecure,
                SameSite = SameSiteMode.Strict,
                Expires = expires
            });

            Response.Cookies.Append("ksignals_name", response.Name, new CookieOptions
            {
                HttpOnly = false,
                Secure = isSecure,
                SameSite = SameSiteMode.Strict,
                Expires = expires
            });

            Response.Cookies.Append("ksignals_email", response.Email, new CookieOptions
            {
                HttpOnly = false,
                Secure = isSecure,
                SameSite = SameSiteMode.Strict,
                Expires = expires
            });
        }

        // Redirect to home page on success
        return Redirect("/");
    }
}
