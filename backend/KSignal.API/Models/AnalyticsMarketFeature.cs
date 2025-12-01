using System.ComponentModel.DataAnnotations;

namespace KSignal.API.Models;

/// <summary>
/// Stores computed market features for analytics and ML purposes.
/// Populated by ProcessMarketAnalyticsConsumer from market_highpriority tickers.
/// </summary>
public class AnalyticsMarketFeature
{
    /// <summary>
    /// Auto-generated feature ID
    /// </summary>
    [Key]
    public ulong FeatureId { get; set; }

    /// <summary>
    /// Market ticker (joins to market_snapshots.Ticker)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Ticker { get; set; } = string.Empty;

    /// <summary>
    /// Series ID (joins to market_series.Ticker)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string SeriesId { get; set; } = string.Empty;

    /// <summary>
    /// Event ticker (joins to market_events.EventTicker)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string EventTicker { get; set; } = string.Empty;

    /// <summary>
    /// When features were generated (usually = snapshot GenerateDate)
    /// </summary>
    public DateTime FeatureTime { get; set; }

    // Time structure
    /// <summary>
    /// CloseTime - FeatureTime in seconds
    /// </summary>
    public long TimeToCloseSeconds { get; set; }

    /// <summary>
    /// ExpectedExpirationTime - FeatureTime in seconds (if not null)
    /// </summary>
    public long TimeToExpirationSeconds { get; set; }

    // Prices in probability space (0-1)
    /// <summary>
    /// YesBid / 100.0
    /// </summary>
    public double YesBidProb { get; set; }

    /// <summary>
    /// YesAsk / 100.0
    /// </summary>
    public double YesAskProb { get; set; }

    /// <summary>
    /// NoBid / 100.0
    /// </summary>
    public double NoBidProb { get; set; }

    /// <summary>
    /// NoAsk / 100.0
    /// </summary>
    public double NoAskProb { get; set; }

    /// <summary>
    /// (YesBidProb + YesAskProb) / 2
    /// </summary>
    public double MidProb { get; set; }

    /// <summary>
    /// Primary market-implied probability for Yes
    /// </summary>
    public double ImpliedProbYes { get; set; }

    // Volatility / change features
    /// <summary>
    /// % price change over last 1h
    /// </summary>
    public double Return1h { get; set; }

    /// <summary>
    /// % price change over last 24h
    /// </summary>
    public double Return24h { get; set; }

    /// <summary>
    /// Realized volatility over 1h window
    /// </summary>
    public double Volatility1h { get; set; }

    /// <summary>
    /// Realized volatility over 24h window
    /// </summary>
    public double Volatility24h { get; set; }

    // Liquidity & depth
    /// <summary>
    /// YesAskProb - YesBidProb
    /// </summary>
    public double BidAskSpread { get; set; }

    /// <summary>
    /// Top of book liquidity for Yes side
    /// </summary>
    public double TopOfBookLiquidityYes { get; set; }

    /// <summary>
    /// Top of book liquidity for No side
    /// </summary>
    public double TopOfBookLiquidityNo { get; set; }

    /// <summary>
    /// Total Yes side liquidity from orderbook
    /// </summary>
    public double TotalLiquidityYes { get; set; }

    /// <summary>
    /// Total No side liquidity from orderbook
    /// </summary>
    public double TotalLiquidityNo { get; set; }

    /// <summary>
    /// (TotalLiquidityYes - TotalLiquidityNo) / (TotalLiquidityYes + TotalLiquidityNo)
    /// </summary>
    public double OrderbookImbalance { get; set; }

    // Volume / activity
    /// <summary>
    /// Trades in last 1h
    /// </summary>
    public double Volume1h { get; set; }

    /// <summary>
    /// Trades in last 24h
    /// </summary>
    public double Volume24h { get; set; }

    /// <summary>
    /// Open interest from market_snapshots
    /// </summary>
    public double OpenInterest { get; set; }

    /// <summary>
    /// Volume * price in last 1h
    /// </summary>
    public double Notional1h { get; set; }

    /// <summary>
    /// Volume * price in last 24h
    /// </summary>
    public double Notional24h { get; set; }

    // Categorical / one-hot style
    /// <summary>
    /// Category from market_series / events
    /// </summary>
    [MaxLength(255)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Market type from market_snapshots
    /// </summary>
    [MaxLength(50)]
    public string MarketType { get; set; } = string.Empty;

    /// <summary>
    /// Active/Closed/Settled status
    /// </summary>
    [MaxLength(64)]
    public string Status { get; set; } = string.Empty;

    // External / factual probability placeholder
    /// <summary>
    /// Real-world estimate of the true probability of "Yes"
    /// </summary>
    public double FactualProbabilityYes { get; set; }

    /// <summary>
    /// 0-1 float: how "wrong" the market price is vs FactualProbabilityYes
    /// </summary>
    public double MispriceScore { get; set; }

    /// <summary>
    /// When this feature row was written
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}
