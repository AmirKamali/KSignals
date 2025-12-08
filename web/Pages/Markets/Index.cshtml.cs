using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using web_asp.Models;
using web_asp.Services;

namespace web_asp.Pages.Markets;

public class IndexModel : PageModel
{
    private readonly BackendClient _backendClient;

    public MarketTableViewModel TableModel { get; private set; } = new();

    public IndexModel(BackendClient backendClient)
    {
        _backendClient = backendClient;
    }

    public async Task<IActionResult> OnGetAsync(string? category, string? tag, string? date, string? sort_type, string? direction, int? page, int? pageSize, string? query)
    {
        var activeCategory = string.IsNullOrWhiteSpace(category) ? "All" : category!;
        var activeTag = string.IsNullOrWhiteSpace(tag) ? null : tag;
        var activeDate = string.IsNullOrWhiteSpace(date) ? "next_30_days" : date!;
        var activeSort = string.IsNullOrWhiteSpace(sort_type) ? "Volume24H" : sort_type!;

        // Set default direction based on sort type if not specified
        string sortDirection;
        if (!string.IsNullOrWhiteSpace(direction))
        {
            sortDirection = direction == "asc" ? "asc" : "desc";
        }
        else
        {
            // Default direction based on sort type
            sortDirection = activeSort == "ClosingSoon" ? "asc" : "desc";
        }
        
        // Check if page parameter exists in query string and try to parse it
        // This handles cases where the page parameter might not be bound correctly
        int? pageValue = page;
        if (Request.Query.ContainsKey("page"))
        {
            var pageQueryValue = Request.Query["page"].ToString();
            if (!string.IsNullOrWhiteSpace(pageQueryValue) && int.TryParse(pageQueryValue, out var parsedPage))
            {
                pageValue = parsedPage;
            }
        }
        var currentPage = Math.Max(1, pageValue ?? 1);
        var size = Math.Max(1, pageSize ?? 20);

        // Require authentication for page > 1 or non-default sorting
        var requiresAuth = currentPage > 1 || activeSort != "Volume24H";
        if (requiresAuth)
        {
            var isAuthenticated = IsUserAuthenticated();
            if (!isAuthenticated)
            {
                // Build return URL with all query parameters
                var returnUrl = Request.Path + Request.QueryString;
                return RedirectToPage("/Login", new { returnUrl });
            }
        }

        var queryModel = new MarketQuery
        {
            Category = activeCategory != "All" ? activeCategory : null,
            Tag = activeTag,
            CloseDateType = activeDate,
            Sort = activeSort,
            Direction = sortDirection,
            Page = currentPage,
            PageSize = size,
            Query = string.IsNullOrWhiteSpace(query) ? null : query
        };

        var tagsByCategories = await _backendClient.GetTagsByCategoriesAsync();
        var response = await _backendClient.GetBackendMarketsAsync(queryModel);

        TableModel = new MarketTableViewModel
        {
            Markets = response.Markets,
            TagsByCategories = tagsByCategories,
            ActiveCategory = activeCategory,
            ActiveTag = activeTag,
            ActiveDate = activeDate,
            ActiveSort = activeSort,
            SortDirection = sortDirection,
            CurrentPage = response.CurrentPage > 0 ? response.CurrentPage : currentPage,
            TotalPages = response.TotalPages,
            TotalCount = response.TotalCount,
            PageSize = response.PageSize > 0 ? response.PageSize : size,
            Query = query ?? string.Empty,
            ShowSearch = true
        };

        return Page();
    }

    private bool IsUserAuthenticated()
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
}
