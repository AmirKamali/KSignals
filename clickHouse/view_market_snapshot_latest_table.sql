-- Create the target table for the materialized view
CREATE TABLE IF NOT EXISTS kalshi_signals.market_snapshots_latest
(
    MarketSnapshotID UUID,
    Ticker           String,
    EventTicker      String,
    MarketType       String,
    YesSubTitle      String,
    NoSubTitle       String,
    Status           String,
    Result           String,
    YesBid           Decimal(18, 8),
    YesAsk           Decimal(18, 8),
    NoBid            Decimal(18, 8),
    NoAsk            Decimal(18, 8),
    LastPrice        Decimal(18, 8),
    Volume           Int32,
    Volume24h        Int32,
    OpenInterest     Int32,
    Liquidity        Int32,
    CreatedTime      DateTime,
    OpenTime         DateTime,
    CloseTime        DateTime,
    GenerateDate     DateTime
)
ENGINE = ReplacingMergeTree(GenerateDate)
ORDER BY (Ticker)

