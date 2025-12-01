-- ============================================================================
-- Migration: Drop SeriesId column from market_snapshots table
-- ============================================================================
-- Date: 2025-12-01
-- Description: Remove SeriesId column as market sync now uses category-based approach.
--              SeriesTicker can be looked up from market_events table via EventTicker.
--
-- NOTE: SeriesId is part of ORDER BY key, so we must recreate the table.
-- ============================================================================

-- Step 1: Create new table without SeriesId column
CREATE TABLE IF NOT EXISTS kalshi_signals.market_snapshots_new
(
    `MarketSnapshotID` UUID DEFAULT generateUUIDv4(),
    `Ticker` String,
    `EventTicker` String,
    `MarketType` String,
    `YesSubTitle` String,
    `NoSubTitle` String,
    `CreatedTime` DateTime,
    `OpenTime` DateTime,
    `CloseTime` DateTime,
    `ExpectedExpirationTime` Nullable(DateTime),
    `LatestExpirationTime` DateTime,
    `FeeWaiverExpirationTime` Nullable(DateTime),
    `SettlementTimerSeconds` Int32,
    `SettlementValue` Nullable(Int32),
    `SettlementValueDollars` Nullable(String),
    `Status` String,
    `Result` String,
    `CanCloseEarly` UInt8,
    `EarlyCloseCondition` Nullable(String),
    `ResponsePriceUnits` String,
    `YesBid` Decimal(18, 8),
    `YesBidDollars` String,
    `YesAsk` Decimal(18, 8),
    `YesAskDollars` String,
    `NoBid` Decimal(18, 8),
    `NoBidDollars` String,
    `NoAsk` Decimal(18, 8),
    `NoAskDollars` String,
    `LastPrice` Decimal(18, 8),
    `LastPriceDollars` String,
    `PreviousYesBid` Int32,
    `PreviousYesBidDollars` String,
    `PreviousYesAsk` Int32,
    `PreviousYesAskDollars` String,
    `PreviousPrice` Int32,
    `PreviousPriceDollars` String,
    `Volume` Int32,
    `Volume24h` Int32,
    `OpenInterest` Int32,
    `NotionalValue` Int32,
    `NotionalValueDollars` String,
    `Liquidity` Int32,
    `LiquidityDollars` String,
    `ExpirationValue` String,
    `TickSize` Int32,
    `StrikeType` Nullable(String),
    `FloorStrike` Nullable(Float64),
    `CapStrike` Nullable(Float64),
    `FunctionalStrike` Nullable(String),
    `CustomStrike` Nullable(String),
    `RulesPrimary` String,
    `RulesSecondary` String,
    `MveCollectionTicker` Nullable(String),
    `MveSelectedLegs` Nullable(String),
    `PrimaryParticipantKey` Nullable(String),
    `PriceLevelStructure` String,
    `PriceRanges` Nullable(String),
    `GenerateDate` DateTime,
    
    -- Skip indexes for common query patterns
    -- Note: Ticker is first in ORDER BY, so lookups by Ticker are already fast
    INDEX idx_event_ticker EventTicker TYPE bloom_filter GRANULARITY 1,
    INDEX idx_volume Volume TYPE minmax GRANULARITY 4,
    INDEX idx_volume24h Volume24h TYPE minmax GRANULARITY 4
)
ENGINE = MergeTree()
ORDER BY (Ticker, EventTicker, GenerateDate)
SETTINGS index_granularity = 8192;

-- Step 2: Copy data from old table to new table (excluding SeriesId)
INSERT INTO kalshi_signals.market_snapshots_new
SELECT
    MarketSnapshotID,
    Ticker,
    EventTicker,
    MarketType,
    YesSubTitle,
    NoSubTitle,
    CreatedTime,
    OpenTime,
    CloseTime,
    ExpectedExpirationTime,
    LatestExpirationTime,
    FeeWaiverExpirationTime,
    SettlementTimerSeconds,
    SettlementValue,
    SettlementValueDollars,
    Status,
    Result,
    CanCloseEarly,
    EarlyCloseCondition,
    ResponsePriceUnits,
    YesBid,
    YesBidDollars,
    YesAsk,
    YesAskDollars,
    NoBid,
    NoBidDollars,
    NoAsk,
    NoAskDollars,
    LastPrice,
    LastPriceDollars,
    PreviousYesBid,
    PreviousYesBidDollars,
    PreviousYesAsk,
    PreviousYesAskDollars,
    PreviousPrice,
    PreviousPriceDollars,
    Volume,
    Volume24h,
    OpenInterest,
    NotionalValue,
    NotionalValueDollars,
    Liquidity,
    LiquidityDollars,
    ExpirationValue,
    TickSize,
    StrikeType,
    FloorStrike,
    CapStrike,
    FunctionalStrike,
    CustomStrike,
    RulesPrimary,
    RulesSecondary,
    MveCollectionTicker,
    MveSelectedLegs,
    PrimaryParticipantKey,
    PriceLevelStructure,
    PriceRanges,
    GenerateDate
FROM kalshi_signals.market_snapshots;

-- Step 3: Verify row counts match
-- SELECT count() FROM kalshi_signals.market_snapshots;
-- SELECT count() FROM kalshi_signals.market_snapshots_new;

-- Step 4: Drop old table and rename new table
DROP TABLE kalshi_signals.market_snapshots;
RENAME TABLE kalshi_signals.market_snapshots_new TO kalshi_signals.market_snapshots;

-- Step 5: Verify final schema
-- DESCRIBE TABLE kalshi_signals.market_snapshots;

