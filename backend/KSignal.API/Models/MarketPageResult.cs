namespace KSignal.API.Models;

public class MarketPageResult
{
    public List<MarketCache> Markets { get; set; } = new List<MarketCache>();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
}
