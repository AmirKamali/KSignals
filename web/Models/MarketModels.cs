namespace web_asp.Models;

public class Market
{
    public string Ticker { get; set; } = string.Empty;
    public string EventTicker { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public decimal YesPrice { get; set; }
    public decimal? NoPrice { get; set; }
    public decimal? PreviousYesPrice { get; set; }
    public decimal? PreviousNoPrice { get; set; }
    public decimal Volume { get; set; }
    public decimal OpenInterest { get; set; }
    public decimal Liquidity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? CloseTime { get; set; }
}

public class BackendMarketsResponse
{
    public List<Market> Markets { get; set; } = new();
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public string Sort { get; set; } = "volume";
    public string Direction { get; set; } = "desc";
}

public class MarketQuery
{
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public string? CloseDateType { get; set; } = "next_30_days";
    public string? Query { get; set; }
    public string Sort { get; set; } = "volume";
    public string Direction { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class BackendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:3006";
}
