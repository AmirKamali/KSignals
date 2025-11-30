-- Create tables for Kalshi Signals application
-- Note: ClickHouse doesn't support auto-increment, so IDs will need to be generated manually in application code
-- Using Int64 for ID fields to match .NET int type (though ClickHouse Int64 is 64-bit, .NET int is 32-bit)

-- Create market_categories table
CREATE TABLE IF NOT EXISTS kalshi_signals.market_categories
(
    SeriesId String,
    Category Nullable(String),
    Tags Nullable(String),
    Ticker Nullable(String),
    Title Nullable(String),
    Frequency Nullable(String),
    JsonResponse Nullable(String),
    LastUpdate DateTime
)
ENGINE = MergeTree()
ORDER BY (SeriesId)
SETTINGS index_granularity = 8192;

-- Create market_snapshots table
-- Using Int64 for MarketSnapshotID (will need manual ID generation in app code)
CREATE TABLE IF NOT EXISTS kalshi_signals.market_snapshots
(
    MarketSnapshotID Int64,
    Ticker String,
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
ORDER BY (Ticker, GenerateDate)
SETTINGS index_granularity = 8192;

-- Create TagsCategories table
-- Using Int64 for Id (will need manual ID generation in app code)
CREATE TABLE IF NOT EXISTS kalshi_signals.TagsCategories
(
    Id Int64,
    Category String,
    Tag String,
    LastUpdate DateTime,
    IsDeleted UInt8 DEFAULT 0
)
ENGINE = MergeTree()
ORDER BY (Category, Tag)
SETTINGS index_granularity = 8192;

-- Create Users table
-- Using Int64 for Id (will need manual ID generation in app code)
CREATE TABLE IF NOT EXISTS kalshi_signals.Users
(
    Id Int64,
    FirebaseId String,
    Username Nullable(String),
    FirstName Nullable(String),
    LastName Nullable(String),
    Email Nullable(String),
    IsComnEmailOn UInt8 DEFAULT 0,
    CreatedAt DateTime,
    UpdatedAt DateTime
)
ENGINE = MergeTree()
ORDER BY (FirebaseId)
SETTINGS index_granularity = 8192;

-- Create market_series table
-- Ticker is the primary key (SeriesId)
CREATE TABLE IF NOT EXISTS kalshi_signals.market_series
(
    Ticker String,
    Frequency String,
    Title String,
    Category String,
    Tags Nullable(String),
    SettlementSources Nullable(String),
    ContractUrl Nullable(String),
    ContractTermsUrl Nullable(String),
    ProductMetadata Nullable(String),
    FeeType String,
    FeeMultiplier Float64,
    AdditionalProhibitions Nullable(String),
    LastUpdate DateTime,
    IsDeleted UInt8 DEFAULT 0,
    INDEX idx_market_series_category Category TYPE bloom_filter GRANULARITY 1,
    INDEX idx_market_series_tags Tags TYPE bloom_filter GRANULARITY 1
)
ENGINE = ReplacingMergeTree(LastUpdate)
ORDER BY (Ticker)
SETTINGS index_granularity = 8192;

-- Create market_events table
-- EventTicker is the primary key
CREATE TABLE IF NOT EXISTS kalshi_signals.market_events
(
    EventTicker String,
    SeriesTicker String,
    SubTitle String,
    Title String,
    CollateralReturnType String,
    MutuallyExclusive UInt8,
    Category String,
    StrikeDate Nullable(DateTime),
    StrikePeriod Nullable(String),
    AvailableOnBrokers UInt8,
    ProductMetadata Nullable(String),
    LastUpdate DateTime,
    IsDeleted UInt8 DEFAULT 0,
    INDEX idx_market_events_series_ticker SeriesTicker TYPE bloom_filter GRANULARITY 1,
    INDEX idx_market_events_category Category TYPE bloom_filter GRANULARITY 1
)
ENGINE = ReplacingMergeTree(LastUpdate)
ORDER BY (EventTicker)
SETTINGS index_granularity = 8192;

-- Create market_highpriority table
-- Tracks markets that should have orderbook synced frequently
-- TickerId is the PRIMARY KEY (via ORDER BY) - no duplicates allowed
-- ReplacingMergeTree ensures only one row per TickerId (latest by LastUpdate)
CREATE TABLE IF NOT EXISTS kalshi_signals.market_highpriority
(
    TickerId String,  -- Primary Key
    Priority Int32,
    LastUpdate DateTime,
    INDEX idx_market_highpriority_priority Priority TYPE minmax GRANULARITY 1
)
ENGINE = ReplacingMergeTree(LastUpdate)
ORDER BY (TickerId)  -- This defines the primary key
SETTINGS index_granularity = 8192;

-- Create orderbook_snapshots table
-- Stores point-in-time snapshots of orderbook state
CREATE TABLE IF NOT EXISTS kalshi_signals.orderbook_snapshots
(
    Id Int64,
    MarketId String,
    CapturedAt DateTime,
    YesLevels Nullable(String),
    NoLevels Nullable(String),
    YesDollars Nullable(String),
    NoDollars Nullable(String),
    BestYes Nullable(Float64),
    BestNo Nullable(Float64),
    Spread Nullable(Float64),
    TotalYesLiquidity Float64,
    TotalNoLiquidity Float64,
    INDEX idx_orderbook_snapshots_market_id MarketId TYPE bloom_filter GRANULARITY 1,
    INDEX idx_orderbook_snapshots_captured_at CapturedAt TYPE minmax GRANULARITY 1
)
ENGINE = MergeTree()
ORDER BY (MarketId, CapturedAt)
SETTINGS index_granularity = 8192;

-- Create orderbook_events table
-- Stores computed changes between orderbook snapshots
CREATE TABLE IF NOT EXISTS kalshi_signals.orderbook_events
(
    Id Int64,
    MarketId String,
    EventTime DateTime,
    Side String,
    Price Float64,
    Size Float64,
    EventType String,
    INDEX idx_orderbook_events_market_id MarketId TYPE bloom_filter GRANULARITY 1,
    INDEX idx_orderbook_events_event_time EventTime TYPE minmax GRANULARITY 1
)
ENGINE = MergeTree()
ORDER BY (MarketId, EventTime)
SETTINGS index_granularity = 8192;
