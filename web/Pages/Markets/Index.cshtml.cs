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

    public async Task OnGetAsync(string? category, string? tag, string? date, string? sort_type, string? direction, int? page, int? pageSize, string? query)
    {
        var activeCategory = string.IsNullOrWhiteSpace(category) ? "All" : category!;
        var activeTag = string.IsNullOrWhiteSpace(tag) ? null : tag;
        var activeDate = string.IsNullOrWhiteSpace(date) ? "next_30_days" : date!;
        var activeSort = string.IsNullOrWhiteSpace(sort_type) ? "volume" : sort_type!;
        var sortDirection = direction == "asc" ? "asc" : "desc";
        var currentPage = Math.Max(1, page ?? 1);
        var size = Math.Max(1, pageSize ?? 20);

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
    }
}
