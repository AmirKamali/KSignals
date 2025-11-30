-- Migration: Add FetchCandlesticks and FetchOrderbook columns to market_highpriority
-- and create market_candlesticks table

-- Step 1: Alter market_highpriority table to add new columns
-- Note: ClickHouse ALTER TABLE ADD COLUMN doesn't support AFTER clause
-- Columns will be added at the end

ALTER TABLE kalshi_signals.market_highpriority ADD COLUMN IF NOT EXISTS FetchCandlesticks UInt8 DEFAULT 1;
ALTER TABLE kalshi_signals.market_highpriority ADD COLUMN IF NOT EXISTS FetchOrderbook UInt8 DEFAULT 1;

-- Step 2: Set FetchCandlesticks and FetchOrderbook to true for all existing data
-- In ClickHouse, we need to use ALTER TABLE UPDATE for this
ALTER TABLE kalshi_signals.market_highpriority UPDATE FetchCandlesticks = 1, FetchOrderbook = 1 WHERE 1=1;

-- Step 3: Create market_candlesticks table
CREATE TABLE IF NOT EXISTS kalshi_signals.market_candlesticks
(
    Id Int64,
    Ticker String,
    SeriesTicker String,
    PeriodInterval Int32,
    EndPeriodTs Int64,
    EndPeriodTime DateTime,
    -- Yes Bid OHLC (in cents)
    YesBidOpen Int32,
    YesBidLow Int32,
    YesBidHigh Int32,
    YesBidClose Int32,
    -- Yes Bid OHLC (in dollars)
    YesBidOpenDollars String,
    YesBidLowDollars String,
    YesBidHighDollars String,
    YesBidCloseDollars String,
    -- Yes Ask OHLC (in cents)
    YesAskOpen Int32,
    YesAskLow Int32,
    YesAskHigh Int32,
    YesAskClose Int32,
    -- Yes Ask OHLC (in dollars)
    YesAskOpenDollars String,
    YesAskLowDollars String,
    YesAskHighDollars String,
    YesAskCloseDollars String,
    -- Price OHLC (nullable, in cents)
    PriceOpen Nullable(Int32),
    PriceLow Nullable(Int32),
    PriceHigh Nullable(Int32),
    PriceClose Nullable(Int32),
    PriceMean Nullable(Int32),
    PricePrevious Nullable(Int32),
    -- Price OHLC (nullable, in dollars)
    PriceOpenDollars Nullable(String),
    PriceLowDollars Nullable(String),
    PriceHighDollars Nullable(String),
    PriceCloseDollars Nullable(String),
    PriceMeanDollars Nullable(String),
    PricePreviousDollars Nullable(String),
    -- Volume and Open Interest
    Volume Int64,
    OpenInterest Int64,
    -- Metadata
    FetchedAt DateTime,
    -- Indexes
    INDEX idx_market_candlesticks_ticker Ticker TYPE bloom_filter GRANULARITY 1,
    INDEX idx_market_candlesticks_series_ticker SeriesTicker TYPE bloom_filter GRANULARITY 1,
    INDEX idx_market_candlesticks_end_period_time EndPeriodTime TYPE minmax GRANULARITY 1
)
ENGINE = MergeTree()
ORDER BY (Ticker, EndPeriodTime, PeriodInterval)
SETTINGS index_granularity = 8192;
