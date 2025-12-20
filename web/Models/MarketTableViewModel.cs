using System.Collections.Generic;
using KSignals.DTO;

namespace web_asp.Models;

public class MarketTableViewModel
{
    public IReadOnlyList<ClientEvent> Markets { get; set; } = Array.Empty<ClientEvent>();
    public Dictionary<string, List<string>> TagsByCategories { get; set; } = new();
    public string ActiveCategory { get; set; } = "All";
    public string? ActiveTag { get; set; }
    public string ActiveDate { get; set; } = "next_30_days";
    public string ActiveSort { get; set; } = "volume";
    public string SortDirection { get; set; } = "desc";
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 20;
    public string Query { get; set; } = string.Empty;
    public bool ShowSearch { get; set; } = true;
    public string? ActiveStrategy { get; set; }
}
