using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using web_asp.Services;

namespace web_asp.Pages;

public class SetUsernameModel : PageModel
{
    private readonly BackendClient _backendClient;
    private readonly ILogger<SetUsernameModel> _logger;

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string FirebaseId { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public SetUsernameModel(BackendClient backendClient, ILogger<SetUsernameModel> logger)
    {
        _backendClient = backendClient;
        _logger = logger;
    }

    public void OnGet()
    {
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
            // Store the token in a cookie
            Response.Cookies.Append("ksignals_jwt", response.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            // Store user info in cookies for client-side access
            Response.Cookies.Append("ksignals_username", response.Username, new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            Response.Cookies.Append("ksignals_name", response.Name, new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            Response.Cookies.Append("ksignals_email", response.Email, new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
        }

        // Redirect to home page on success
        return RedirectToPage("/Index");
    }
}
