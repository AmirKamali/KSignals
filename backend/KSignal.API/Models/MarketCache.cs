using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KSignal.API.Models;

public class MarketCache
{
    [Key]
    public string TickerId { get; set; } = string.Empty;
    public string SeriesTicker { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public int Volume { get; set; }
    public int Volume24h { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime ExpirationTime { get; set; }
    public DateTime CloseTime { get; set; }
    public DateTime LatestExpirationTime { get; set; }
    public DateTime OpenTime { get; set; }
    public string? Status { get; set; }
    public decimal YesBid { get; set; }
    public string? YesBidDollars { get; set; }
    public decimal YesAsk { get; set; }
    public string? YesAskDollars { get; set; }
    public decimal NoBid { get; set; }
    public string? NoBidDollars { get; set; }
    public decimal NoAsk { get; set; }
    public string? NoAskDollars { get; set; }
    public decimal LastPrice { get; set; }
    public string? LastPriceDollars { get; set; }
    public int PreviousYesBid { get; set; }
    public string? PreviousYesBidDollars { get; set; }
    public int PreviousYesAsk { get; set; }
    public string? PreviousYesAskDollars { get; set; }
    public int PreviousPrice { get; set; }
    public string? PreviousPriceDollars { get; set; }
    public int Liquidity { get; set; }
    public string? LiquidityDollars { get; set; }
    public int? SettlementValue { get; set; }
    public string? SettlementValueDollars { get; set; }
    public int NotionalValue { get; set; }
    public string? NotionalValueDollars { get; set; }
    public string? JsonResponse { get; set; }
    public DateTime LastUpdate { get; set; }
}

