using System;

namespace KSignals.DTO;

/// <summary>
/// Combined view of Event and MarketSnapshot data for client consumption
/// </summary>
public class ClientEvent
{
    // Event fields
    public string EventTicker { get; set; } = string.Empty;
    public string SeriesTicker { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    // Market identification
    public string Ticker { get; set; } = string.Empty;
    public string MarketType { get; set; } = string.Empty;
    public string YesSubTitle { get; set; } = string.Empty;
    public string NoSubTitle { get; set; } = string.Empty;
    
    // Time fields
    public DateTime CreatedTime { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
    public DateTime? ExpectedExpirationTime { get; set; }
    public DateTime LatestExpirationTime { get; set; }
    public string Status { get; set; } = string.Empty;
    
    // Pricing fields
    public decimal YesBid { get; set; }
    public string YesBidDollars { get; set; } = string.Empty;
    public decimal YesAsk { get; set; }
    public string YesAskDollars { get; set; } = string.Empty;
    public decimal NoBid { get; set; }
    public string NoBidDollars { get; set; } = string.Empty;
    public decimal NoAsk { get; set; }
    public string NoAskDollars { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public string LastPriceDollars { get; set; } = string.Empty;
    public int PreviousYesBid { get; set; }
    public string PreviousYesBidDollars { get; set; } = string.Empty;
    public int PreviousYesAsk { get; set; }
    public string PreviousYesAskDollars { get; set; } = string.Empty;
    public int PreviousPrice { get; set; }
    public string PreviousPriceDollars { get; set; } = string.Empty;
    public int? SettlementValue { get; set; }
    public string? SettlementValueDollars { get; set; }
    
    // Volume fields
    public long Volume { get; set; }
    public long Volume24h { get; set; }
    public long OpenInterest { get; set; }
    public long NotionalValue { get; set; }
    public string NotionalValueDollars { get; set; } = string.Empty;

    // Liquidity fields
    public long Liquidity { get; set; }
    public string LiquidityDollars { get; set; } = string.Empty;
    
    // Metadata
    public DateTime GenerateDate { get; set; }
}

/// <summary>
/// Paginated response containing ClientEvent items
/// </summary>
public class ClientEventPageResult
{
    public List<ClientEvent> Markets { get; set; } = new List<ClientEvent>();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
}

