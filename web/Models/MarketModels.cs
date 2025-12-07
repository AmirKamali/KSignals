using KSignals.DTO;

namespace web_asp.Models;

public class BackendMarketsResponse
{
    public List<ClientEvent> Markets { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public string Sort { get; set; } = "Volume24H";
    public string Direction { get; set; } = "desc";
}

public class MarketQuery
{
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public string? CloseDateType { get; set; } = "next_30_days";
    public string? Query { get; set; }
    public string Sort { get; set; } = "Volume24H";
    public string Direction { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class BackendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:3006";
    public string PublicBaseUrl { get; set; } = "http://localhost:3006";
}
