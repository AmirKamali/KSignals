-- Migration: Add SeriesId column to market_snapshots table
-- This migration deletes all existing data and adds SeriesId column after Ticker

-- Step 1: Delete all existing data from market_snapshots
TRUNCATE TABLE kalshi_signals.market_snapshots;

-- Step 2: Add SeriesId column after Ticker
-- Note: ClickHouse doesn't support AFTER clause in ALTER TABLE ADD COLUMN
-- The column will be added at the end, but we'll recreate the table with proper column order

-- Drop the existing table
DROP TABLE IF EXISTS kalshi_signals.market_snapshots;

-- Recreate the table with SeriesId after Ticker
CREATE TABLE IF NOT EXISTS kalshi_signals.market_snapshots
(
    MarketSnapshotID Int64,
    Ticker String,
    SeriesId String,
    EventTicker String,
    MarketType String,
    YesSubTitle String,
    NoSubTitle String,
    CreatedTime DateTime,
    OpenTime DateTime,
    CloseTime DateTime,
    ExpectedExpirationTime Nullable(DateTime),
    LatestExpirationTime DateTime,
    SettlementTimerSeconds Int32,
    Status String,
    ResponsePriceUnits String,
    YesBid Decimal64(8),
    YesBidDollars String,
    YesAsk Decimal64(8),
    YesAskDollars String,
    NoBid Decimal64(8),
    NoBidDollars String,
    NoAsk Decimal64(8),
    NoAskDollars String,
    LastPrice Decimal64(8),
    LastPriceDollars String,
    Volume Int32,
    Volume24h Int32,
    Result String,
    CanCloseEarly UInt8,
    OpenInterest Int32,
    NotionalValue Int32,
    NotionalValueDollars String,
    PreviousYesBid Int32,
    PreviousYesBidDollars String,
    PreviousYesAsk Int32,
    PreviousYesAskDollars String,
    PreviousPrice Int32,
    PreviousPriceDollars String,
    Liquidity Int32,
    LiquidityDollars String,
    SettlementValue Nullable(Int32),
    SettlementValueDollars Nullable(String),
    ExpirationValue String,
    FeeWaiverExpirationTime Nullable(DateTime),
    EarlyCloseCondition Nullable(String),
    TickSize Int32,
    StrikeType Nullable(String),
    FloorStrike Nullable(Float64),
    CapStrike Nullable(Float64),
    FunctionalStrike Nullable(String),
    CustomStrike Nullable(String),
    RulesPrimary String,
    RulesSecondary String,
    MveCollectionTicker Nullable(String),
    MveSelectedLegs Nullable(String),
    PrimaryParticipantKey Nullable(String),
    PriceLevelStructure String,
    PriceRanges Nullable(String),
    GenerateDate DateTime
)
ENGINE = MergeTree()
ORDER BY (Ticker, SeriesId, GenerateDate)
SETTINGS index_granularity = 8192;
