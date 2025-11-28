using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace web_asp.Pages.User;

public abstract class AuthenticatedPageModel : PageModel
{
    protected virtual bool RequireFullAuthentication => true;

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        base.OnPageHandlerExecuting(context);

        // Check if user is authenticated via Firebase ID or JWT in cookies
        var isAuthenticated = IsUserAuthenticated();

        if (!isAuthenticated)
        {
            // User is not authenticated, redirect to login with return URL
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            context.Result = new RedirectToPageResult("/Login", new { returnUrl });
        }
    }

    protected bool IsUserAuthenticated()
    {
        // Try to get Firebase ID from cookie
        if (Request.Cookies.TryGetValue("ksignals_firebase_id", out var firebaseId) &&
            !string.IsNullOrWhiteSpace(firebaseId))
        {
            return true;
        }

        // Check if JWT token exists (indicates full authentication)
        if (Request.Cookies.TryGetValue("ksignals_jwt", out var jwt) && !string.IsNullOrWhiteSpace(jwt))
        {
            return true;
        }

        return false;
    }

    protected string? GetFirebaseIdFromCookies()
    {
        // Try to get Firebase ID from cookie
        if (Request.Cookies.TryGetValue("ksignals_firebase_id", out var firebaseId))
        {
            return firebaseId;
        }

        return null;
    }
}
