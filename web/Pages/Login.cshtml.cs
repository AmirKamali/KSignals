using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace web_asp.Pages;

public class LoginModel : PageModel
{
    public string? ReturnUrl { get; set; }
    public bool IsFromMarkets { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";
        
        // Check if the return URL is from /markets page
        IsFromMarkets = !string.IsNullOrWhiteSpace(returnUrl) && 
                       returnUrl.Contains("/markets", StringComparison.OrdinalIgnoreCase);
    }
}
