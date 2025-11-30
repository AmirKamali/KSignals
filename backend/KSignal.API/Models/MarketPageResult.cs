namespace KSignal.API.Models;

public class MarketPageResult
{
    public List<MarketSnapshot> Markets { get; set; } = new List<MarketSnapshot>();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
}
