namespace web_asp.Models;

public class MarketDetails : Market
{
    public decimal? YesBid { get; set; }
    public decimal? YesAsk { get; set; }
    public decimal? NoBid { get; set; }
    public decimal? NoAsk { get; set; }
    public decimal? LastPrice { get; set; }
    public decimal? Volume24h { get; set; }
    public string? OpenTime { get; set; }
    public string? ExpirationTime { get; set; }
    public string? LatestExpirationTime { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class KalshiOptions
{
    public string BaseUrl { get; set; } = "https://trading-api.kalshi.com/trade-api/v2";
}
