using System.ComponentModel.DataAnnotations;


namespace KSignal.API.Models;

public class MarketSnapshot
{
    [Key]
    public Guid MarketSnapshotID { get; set; }

    public string Ticker { get; set; } = string.Empty;
    public string EventTicker { get; set; } = string.Empty;
    public string MarketType { get; set; } = string.Empty;
    public string YesSubTitle { get; set; } = string.Empty;
    public string NoSubTitle { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
    public DateTime? ExpectedExpirationTime { get; set; }
    public DateTime LatestExpirationTime { get; set; }
    public DateTime? FeeWaiverExpirationTime { get; set; }
    public int? SettlementValue { get; set; }
    public decimal? SettlementValueDollars { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool CanCloseEarly { get; set; }
    public string ResponsePriceUnits { get; set; } = string.Empty;
    public decimal YesBid { get; set; }
    public decimal YesBidDollars { get; set; }
    public decimal YesAsk { get; set; }
    public decimal YesAskDollars { get; set; }
    public decimal NoBid { get; set; }
    public decimal NoBidDollars { get; set; }
    public decimal NoAsk { get; set; }
    public decimal NoAskDollars { get; set; }
    public decimal LastPrice { get; set; }
    public decimal LastPriceDollars { get; set; }
    public int PreviousYesBid { get; set; }
    public decimal PreviousYesBidDollars { get; set; }
    public int PreviousYesAsk { get; set; }
    public decimal PreviousYesAskDollars { get; set; }
    public int PreviousPrice { get; set; }
    public decimal PreviousPriceDollars { get; set; }
    public int Volume { get; set; }
    public int Volume24h { get; set; }
    public int OpenInterest { get; set; }
    public int NotionalValue { get; set; }
    public decimal NotionalValueDollars { get; set; }
    public int Liquidity { get; set; }
    public decimal LiquidityDollars { get; set; }
    public string ExpirationValue { get; set; } = string.Empty;
    public int TickSize { get; set; }
    public string? StrikeType { get; set; }
    public double? FloorStrike { get; set; }
    public double? CapStrike { get; set; }
    public string? FunctionalStrike { get; set; }
    public string? CustomStrike { get; set; } // JSON string
    public string? MveCollectionTicker { get; set; }
    public string? MveSelectedLegs { get; set; } // JSON string
    public string? PrimaryParticipantKey { get; set; }
    public string PriceLevelStructure { get; set; } = string.Empty;
    public string? PriceRanges { get; set; } // JSON string
    public DateTime GenerateDate { get; set; }

    // Additional properties specific to market_snapshots table
    public int SettlementTimerSeconds { get; set; }
    public string? EarlyCloseCondition { get; set; }
    public string RulesPrimary { get; set; } = string.Empty;
    public string RulesSecondary { get; set; } = string.Empty;
}
