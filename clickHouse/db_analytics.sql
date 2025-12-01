CREATE TABLE IF NOT EXISTS kalshi_signals.analytics_market_features
(
    `FeatureId` UInt64 DEFAULT generateSerialID('analytics_market_features'),

    -- Keys / joins
    `Ticker` String,                -- Market ticker (joins to market_snapshots.Ticker)
    `SeriesId` String,              -- Series (joins to market_series.Ticker)
    `EventTicker` String,           -- Event (joins to market_events.EventTicker)
    `FeatureTime` DateTime,         -- When features were generated (usually = snapshot GenerateDate)

    -- Time structure
    `TimeToCloseSeconds` Int64,     -- CloseTime - FeatureTime
    `TimeToExpirationSeconds` Int64,-- ExpectedExpirationTime - FeatureTime (if not null)

    -- Prices in probability space (0-1)
    `YesBidProb` Float64,           -- YesBid / 100.0
    `YesAskProb` Float64,           -- YesAsk / 100.0
    `NoBidProb` Float64,            -- NoBid / 100.0
    `NoAskProb` Float64,            -- NoAsk / 100.0
    `MidProb` Float64,              -- (YesBidProb + YesAskProb) / 2
    `ImpliedProbYes` Float64,       -- Primary market-implied prob for Yes (can be MidProb or modelled)

    -- Volatility / change features (from candlesticks or diffs of snapshots)
    `Return1h` Float64,             -- % price change over last 1h
    `Return24h` Float64,            -- % price change over last 24h
    `Volatility1h` Float64,         -- Realized volatility over 1h window
    `Volatility24h` Float64,        -- Realized volatility over 24h window

    -- Liquidity & depth (from snapshots / orderbook_snapshots)
    `BidAskSpread` Float64,         -- (YesAskProb - YesBidProb)
    `TopOfBookLiquidityYes` Float64,-- Derived from best levels / size if available
    `TopOfBookLiquidityNo` Float64,
    `TotalLiquidityYes` Float64,    -- From orderbook_snapshots.TotalYesLiquidity
    `TotalLiquidityNo` Float64,     -- From orderbook_snapshots.TotalNoLiquidity
    `OrderbookImbalance` Float64,   -- (TotalLiquidityYes - TotalLiquidityNo) / (TotalLiquidityYes + TotalLiquidityNo)

    -- Volume / activity
    `Volume1h` Float64,             -- Trades in last 1h
    `Volume24h` Float64,            -- Trades in last 24h
    `OpenInterest` Float64,         -- From market_snapshots
    `Notional1h` Float64,           -- Volume * price in last 1h
    `Notional24h` Float64,          -- Volume * price in last 24h

    -- Categorical / one-hot style
    `Category` String,              -- From market_series / events
    `MarketType` String,            -- From market_snapshots.MarketType
    `Status` String,                -- Active/Closed/Settled (from snapshots)

    -- External / factual probability placeholder
    `FactualProbabilityYes` Float64 DEFAULT 0.0,
    -- Real-world estimate of the true probability of "Yes".
    -- Examples of sources:
    --   - External betting markets or brokers
    --   - Polling models
    --   - Your own fundamental model of the event
    -- This is used for backtesting and mispricing detection.

    `MispriceScore` Float64 DEFAULT 0.0,
    -- 0-1 float: how "wrong" the market price is vs FactualProbabilityYes.
    -- Example:
    --   diff = abs(ImpliedProbYes - FactualProbabilityYes)
    --   MispriceScore = min(1.0, diff / 0.5)  -- cap at 1 if diff >= 0.5

    `GeneratedAt` DateTime          -- When this feature row was written
)
ENGINE = MergeTree()
ORDER BY (Ticker, FeatureTime)
SETTINGS index_granularity = 8192;



CREATE TABLE IF NOT EXISTS kalshi_signals.analytics_market_metrics
(
    `MetricId` UInt64 DEFAULT generateSerialID('analytics_market_metrics'),

    `Ticker` String,
    `SeriesId` String,
    `EventTicker` String,
    `MetricDate` Date,              -- Usually the trading day or evaluation day

    -- Outcome / realized result
    `IsSettled` UInt8,              -- 1 if market settled by this MetricDate
    `RealizedYes` UInt8,            -- 1 if Yes won, 0 if No won (after settlement)
    `SettlementPriceYes` Float64,   -- 1.0 or 0.0 in probability terms (100 or 0 cents)

    -- Market-implied vs realized (for calibration)
    `AvgImpliedProbYes` Float64,    -- Average ImpliedProbYes during the period
    `MedianImpliedProbYes` Float64,
    `LastImpliedProbYes` Float64,   -- Implied prob at close of MetricDate

    -- External / factual probability (real world)
    `FactualProbabilityYes` Float64,
    -- Same concept as in features, but aggregated per day or per evaluation.
    -- You can:
    --   - Pull from external data feeds (e.g., polling model update per day)
    --   - Compute from cohort statistics (e.g., similar events historically)

    -- Mispricing evaluation
    `AvgMispriceScore` Float64,     -- Average mispricing during the period
    `MaxMispriceScore` Float64,     -- Worst mispricing
    `LastMispriceScore` Float64,    -- misprice_score at the end of the day

    -- Performance metrics for signals (if you run strategies)
    `PnL_Per_Contract` Float64,     -- Strategy PnL per contract for this ticker on this day
    `PnL_Cumulative` Float64,       -- Running cumulative PnL per ticker
    `Sharpe1m` Float64,             -- Rolling Sharpe over 1 month
    `HitRate` Float64,              -- Fraction of times your signal direction was correct

    `GeneratedAt` DateTime
)
ENGINE = MergeTree()
ORDER BY (Ticker, MetricDate)
SETTINGS index_granularity = 8192;
