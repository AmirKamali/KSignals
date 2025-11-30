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
