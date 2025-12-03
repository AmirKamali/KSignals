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
    public string? SettlementValueDollars { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool CanCloseEarly { get; set; }
    public string ResponsePriceUnits { get; set; } = string.Empty;
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
    public int Volume { get; set; }
    public int Volume24h { get; set; }
    public int OpenInterest { get; set; }
    public int NotionalValue { get; set; }
    public string NotionalValueDollars { get; set; } = string.Empty;
    public int Liquidity { get; set; }
    public string LiquidityDollars { get; set; } = string.Empty;
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
