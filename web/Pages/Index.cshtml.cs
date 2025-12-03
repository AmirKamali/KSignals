using Microsoft.AspNetCore.Mvc.RazorPages;
using web_asp.Models;
using web_asp.Services;
using KSignals.DTO;

namespace web_asp.Pages;

public class IndexModel : PageModel
{
    private readonly BackendClient _backendClient;

    public IReadOnlyList<ClientEvent> TopMarkets { get; private set; } = Array.Empty<ClientEvent>();
    public MarketTableViewModel TableModel { get; private set; } = new();

    public IndexModel(BackendClient backendClient)
    {
        _backendClient = backendClient;
    }

    public async Task OnGet()
    {
        var markets = await _backendClient.GetHighVolumeMarketsAsync(100);
        var tagsByCategories = await _backendClient.GetTagsByCategoriesAsync();

        TopMarkets = markets.Take(6).ToList();
        var tableMarkets = markets.Skip(6).ToList();
        if (tableMarkets.Count == 0)
        {
            tableMarkets = markets.ToList();
        }

        const int pageSize = 20;
        var pagedMarkets = tableMarkets.Take(pageSize).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(tableMarkets.Count / (double)pageSize));

        TableModel = new MarketTableViewModel
        {
            Markets = pagedMarkets,
            TagsByCategories = tagsByCategories,
            ActiveCategory = "All",
            ActiveDate = "next_30_days",
            ActiveSort = "volume",
            SortDirection = "desc",
            TotalPages = totalPages,
            CurrentPage = 1,
            TotalCount = tableMarkets.Count,
            PageSize = pageSize,
            Query = string.Empty,
            ShowSearch = true
        };
    }
}
