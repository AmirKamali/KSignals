-- =============================================================================
-- KALSHI SIGNALS DATABASE SCHEMA
-- ClickHouse Database Script
-- =============================================================================
-- This script contains the complete schema for all tables in the Kalshi Signals
-- application. ClickHouse is used as the primary data store for market data,
-- orderbook snapshots, candlestick data, and user information.
-- =============================================================================

-- Create database if not exists
CREATE DATABASE IF NOT EXISTS kalshi_signals;

-- =============================================================================
-- TABLE: market_snapshots
-- =============================================================================
-- PURPOSE: Stores point-in-time snapshots of market data from Kalshi API.
--          This is the primary table for market information including prices,
--          volume, and market metadata.
-- 
-- USAGE:
--   - Populated by: SynchronizationService.InsertMarketSnapshotsAsync()
--   - API Endpoint: POST /api/private/data-source/sync-market-snapshots
--   - Used for: Historical price analysis, market status tracking, trend detection
--   - Query patterns: Filter by Ticker, Status, time ranges on GenerateDate
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_snapshots
(
    -- Primary identifier (auto-generated, requires ZooKeeper in production)
    `MarketSnapshotID` UInt64 DEFAULT generateSerialID('market_snapshots'),
    
    -- Market identifiers
    `Ticker` String,                              -- Unique market ticker (e.g., 'BEYONCEGENRE-30-AFA')
    `SeriesId` String,                            -- Parent series identifier (e.g., 'KXBEYONCEGENRE')
    `EventTicker` String,                         -- Associated event ticker
    
    -- Market metadata
    `MarketType` String,                          -- Type of market (binary, etc.)
    `YesSubTitle` String,                         -- Display subtitle for Yes outcome
    `NoSubTitle` String,                          -- Display subtitle for No outcome
    
    -- Timestamps
    `CreatedTime` DateTime,                       -- When market was created on Kalshi
    `OpenTime` DateTime,                          -- When market opened for trading
    `CloseTime` DateTime,                         -- When market closes/closed
    `ExpectedExpirationTime` Nullable(DateTime),  -- Expected settlement time
    `LatestExpirationTime` DateTime,              -- Latest possible expiration
    `FeeWaiverExpirationTime` Nullable(DateTime), -- Fee waiver end time
    
    -- Settlement info
    `SettlementTimerSeconds` Int32,               -- Seconds until settlement
    `SettlementValue` Nullable(Int32),            -- Final settlement value (cents)
    `SettlementValueDollars` Nullable(String),    -- Final settlement value (dollars)
    
    -- Market status
    `Status` String,                              -- Market status: 'Active', 'Closed', 'Settled'
    `Result` String,                              -- Outcome result after settlement
    `CanCloseEarly` UInt8,                        -- Boolean: can market close early
    `EarlyCloseCondition` Nullable(String),       -- Condition for early close
    
    -- Current pricing (in cents and dollars)
    `ResponsePriceUnits` String,                  -- Price unit type from API
    `YesBid` Decimal(18, 8),                      -- Best Yes bid price (cents)
    `YesBidDollars` String,                       -- Best Yes bid price (dollars)
    `YesAsk` Decimal(18, 8),                      -- Best Yes ask price (cents)
    `YesAskDollars` String,                       -- Best Yes ask price (dollars)
    `NoBid` Decimal(18, 8),                       -- Best No bid price (cents)
    `NoBidDollars` String,                        -- Best No bid price (dollars)
    `NoAsk` Decimal(18, 8),                       -- Best No ask price (cents)
    `NoAskDollars` String,                        -- Best No ask price (dollars)
    `LastPrice` Decimal(18, 8),                   -- Last traded price (cents)
    `LastPriceDollars` String,                    -- Last traded price (dollars)
    
    -- Previous prices (for change calculation)
    `PreviousYesBid` Int32,                       -- Previous Yes bid (cents)
    `PreviousYesBidDollars` String,
    `PreviousYesAsk` Int32,                       -- Previous Yes ask (cents)
    `PreviousYesAskDollars` String,
    `PreviousPrice` Int32,                        -- Previous last price (cents)
    `PreviousPriceDollars` String,
    
    -- Volume and liquidity
    `Volume` Int32,                               -- Total contracts traded
    `Volume24h` Int32,                            -- Contracts traded in last 24h
    `OpenInterest` Int32,                         -- Open contract positions
    `NotionalValue` Int32,                        -- Total notional value (cents)
    `NotionalValueDollars` String,
    `Liquidity` Int32,                            -- Available liquidity (cents)
    `LiquidityDollars` String,
    
    -- Strike/price structure
    `ExpirationValue` String,                     -- Value at expiration
    `TickSize` Int32,                             -- Minimum price increment
    `StrikeType` Nullable(String),                -- Type of strike
    `FloorStrike` Nullable(Float64),              -- Floor strike price
    `CapStrike` Nullable(Float64),                -- Cap strike price
    `FunctionalStrike` Nullable(String),          -- Functional strike definition
    `CustomStrike` Nullable(String),              -- Custom strike (JSON string)
    
    -- Rules and metadata
    `RulesPrimary` String,                        -- Primary market rules
    `RulesSecondary` String,                      -- Secondary market rules
    `MveCollectionTicker` Nullable(String),       -- MVE collection ticker
    `MveSelectedLegs` Nullable(String),           -- MVE selected legs (JSON)
    `PrimaryParticipantKey` Nullable(String),     -- Primary participant
    `PriceLevelStructure` String,                 -- Price level structure
    `PriceRanges` Nullable(String),               -- Price ranges (JSON)
    
    -- Record metadata
    `GenerateDate` DateTime                       -- When this snapshot was captured
)
ENGINE = MergeTree()
ORDER BY (Ticker, SeriesId, GenerateDate)
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: market_highpriority
-- =============================================================================
-- PURPOSE: Stores list of high-priority markets for enhanced data collection.
--          Markets in this table get additional data fetching (candlesticks,
--          orderbook depth) beyond standard snapshots.
-- 
-- USAGE:
--   - Managed by: Admin API or manual insertion
--   - Used by: SynchronizeCandlesticksConsumer, SynchronizeOrderbookConsumer
--   - Controls: Which markets get candlestick and orderbook data
--   - Query patterns: Filter by FetchCandlesticks, FetchOrderbook flags
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_highpriority
(
    `TickerId` String,                            -- Market ticker ID (primary key)
    `Priority` Int32,                             -- Priority level (higher = more important)
    `LastUpdate` DateTime,                        -- Last modification timestamp
    `FetchCandlesticks` UInt8 DEFAULT 1,          -- Boolean: fetch candlestick data
    `FetchOrderbook` UInt8 DEFAULT 1,             -- Boolean: fetch orderbook data
    `ProcessAnalyticsL1` UInt8 DEFAULT 1,         -- Boolean: process L1 analytics (basic features)
    `ProcessAnalyticsL2` UInt8 DEFAULT 1,         -- Boolean: process L2 analytics (volatility/returns)
    `ProcessAnalyticsL3` UInt8 DEFAULT 1,         -- Boolean: process L3 analytics (advanced metrics)
    
    INDEX idx_market_highpriority_priority Priority TYPE minmax GRANULARITY 1
)
ENGINE = ReplacingMergeTree(LastUpdate)           -- Deduplicates by TickerId, keeps latest
ORDER BY TickerId
SETTINGS index_granularity = 8192;

-- Migration: Add process analytics columns to existing data (run once)
-- ALTER TABLE kalshi_signals.market_highpriority ADD COLUMN IF NOT EXISTS `ProcessAnalyticsL1` UInt8 DEFAULT 1;
-- ALTER TABLE kalshi_signals.market_highpriority ADD COLUMN IF NOT EXISTS `ProcessAnalyticsL2` UInt8 DEFAULT 1;
-- ALTER TABLE kalshi_signals.market_highpriority ADD COLUMN IF NOT EXISTS `ProcessAnalyticsL3` UInt8 DEFAULT 1;


-- =============================================================================
-- TABLE: market_candlesticks
-- =============================================================================
-- PURPOSE: Stores OHLC (Open-High-Low-Close) candlestick data for markets.
--          Used for charting, technical analysis, and price trend detection.
-- 
-- USAGE:
--   - Populated by: SynchronizationService.SynchronizeCandlesticksAsync()
--   - API Endpoint: POST /api/private/data-source/sync-candlesticks
--   - Data source: Kalshi API GetMarketCandlesticksAsync()
--   - Query patterns: Filter by Ticker, time range on EndPeriodTime
--   - Intervals: 1 minute (1), 1 hour (60), 1 day (1440)
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_candlesticks
(
    `Id` Int64,                                   -- Unique record identifier
    `Ticker` String,                              -- Market ticker ID
    `SeriesTicker` String,                        -- Parent series ticker
    `PeriodInterval` Int32,                       -- Candle interval in minutes (1, 60, 1440)
    `EndPeriodTs` Int64,                          -- End period Unix timestamp
    `EndPeriodTime` DateTime,                     -- End period as DateTime
    
    -- Yes Bid OHLC (in cents)
    `YesBidOpen` Int32,                           -- Opening Yes bid price
    `YesBidLow` Int32,                            -- Lowest Yes bid price
    `YesBidHigh` Int32,                           -- Highest Yes bid price
    `YesBidClose` Int32,                          -- Closing Yes bid price
    
    -- Yes Bid OHLC (in dollars)
    `YesBidOpenDollars` String,
    `YesBidLowDollars` String,
    `YesBidHighDollars` String,
    `YesBidCloseDollars` String,
    
    -- Yes Ask OHLC (in cents)
    `YesAskOpen` Int32,                           -- Opening Yes ask price
    `YesAskLow` Int32,                            -- Lowest Yes ask price
    `YesAskHigh` Int32,                           -- Highest Yes ask price
    `YesAskClose` Int32,                          -- Closing Yes ask price
    
    -- Yes Ask OHLC (in dollars)
    `YesAskOpenDollars` String,
    `YesAskLowDollars` String,
    `YesAskHighDollars` String,
    `YesAskCloseDollars` String,
    
    -- Price OHLC (nullable - no trades may occur during period)
    `PriceOpen` Nullable(Int32),                  -- Opening trade price (cents)
    `PriceLow` Nullable(Int32),                   -- Lowest trade price
    `PriceHigh` Nullable(Int32),                  -- Highest trade price
    `PriceClose` Nullable(Int32),                 -- Closing trade price
    `PriceMean` Nullable(Int32),                  -- Mean trade price
    `PricePrevious` Nullable(Int32),              -- Previous period close
    
    -- Price OHLC (in dollars)
    `PriceOpenDollars` Nullable(String),
    `PriceLowDollars` Nullable(String),
    `PriceHighDollars` Nullable(String),
    `PriceCloseDollars` Nullable(String),
    `PriceMeanDollars` Nullable(String),
    `PricePreviousDollars` Nullable(String),
    
    -- Volume and interest
    `Volume` Int64,                               -- Contracts traded during period
    `OpenInterest` Int64,                         -- Open interest at period end
    
    -- Record metadata
    `FetchedAt` DateTime,                         -- When this data was fetched
    
    -- Indexes for efficient querying
    INDEX idx_market_candlesticks_ticker Ticker TYPE bloom_filter GRANULARITY 1,
    INDEX idx_market_candlesticks_series_ticker SeriesTicker TYPE bloom_filter GRANULARITY 1,
    INDEX idx_market_candlesticks_end_period_time EndPeriodTime TYPE minmax GRANULARITY 1
)
ENGINE = MergeTree()
ORDER BY (Ticker, EndPeriodTime, PeriodInterval)
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: market_series
-- =============================================================================
-- PURPOSE: Stores series metadata from Kalshi. A series groups related markets
--          (e.g., all BTC price markets for different dates).
-- 
-- USAGE:
--   - Populated by: SynchronizationService (series sync)
--   - API Endpoint: POST /api/private/data-source/sync-series
--   - Used for: Grouping markets, category browsing, filtering
--   - Query patterns: Filter by Category, Tags
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_series
(
    `Ticker` String,                              -- Series ticker (primary key)
    `Frequency` String,                           -- Update frequency
    `Title` String,                               -- Human-readable title
    `Category` String,                            -- Category classification
    `Tags` Nullable(String),                      -- Tags (JSON array as string)
    `SettlementSources` Nullable(String),         -- Settlement data sources (JSON)
    `ContractUrl` Nullable(String),               -- Contract details URL
    `ContractTermsUrl` Nullable(String),          -- Terms and conditions URL
    `ProductMetadata` Nullable(String),           -- Additional metadata (JSON)
    `FeeType` String,                             -- Fee structure type
    `FeeMultiplier` Float64,                      -- Fee multiplier value
    `AdditionalProhibitions` Nullable(String),    -- Trading restrictions
    `LastUpdate` DateTime,                        -- Last sync timestamp
    `IsDeleted` UInt8 DEFAULT 0,                  -- Soft delete flag
    
    INDEX idx_market_series_category Category TYPE bloom_filter GRANULARITY 1,
    INDEX idx_market_series_tags Tags TYPE bloom_filter GRANULARITY 1
)
ENGINE = ReplacingMergeTree(LastUpdate)
ORDER BY Ticker
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: market_events
-- =============================================================================
-- PURPOSE: Stores event metadata from Kalshi. Events represent specific
--          occurrences that markets are built around (e.g., "Will BTC hit $100k?").
-- 
-- USAGE:
--   - Populated by: SynchronizationService (events sync)
--   - API Endpoint: POST /api/private/data-source/sync-events
--   - Used for: Event browsing, market grouping, category filtering
--   - Query patterns: Filter by SeriesTicker, Category
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_events
(
    `EventTicker` String,                         -- Event ticker (primary key)
    `SeriesTicker` String,                        -- Parent series ticker
    `SubTitle` String,                            -- Event subtitle
    `Title` String,                               -- Event title
    `CollateralReturnType` String,                -- How collateral is returned
    `MutuallyExclusive` UInt8,                    -- Boolean: outcomes mutually exclusive
    `Category` String,                            -- Event category
    `StrikeDate` Nullable(DateTime),              -- Strike/settlement date
    `StrikePeriod` Nullable(String),              -- Strike period description
    `AvailableOnBrokers` UInt8,                   -- Boolean: available via brokers
    `ProductMetadata` Nullable(String),           -- Additional metadata (JSON)
    `LastUpdate` DateTime,                        -- Last sync timestamp
    `IsDeleted` UInt8 DEFAULT 0,                  -- Soft delete flag
    
    INDEX idx_market_events_series_ticker SeriesTicker TYPE bloom_filter GRANULARITY 1,
    INDEX idx_market_events_category Category TYPE bloom_filter GRANULARITY 1
)
ENGINE = ReplacingMergeTree(LastUpdate)
ORDER BY EventTicker
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: market_categories
-- =============================================================================
-- PURPOSE: Stores category-to-series mapping and cached category data.
--          Used for efficient category-based market browsing.
-- 
-- USAGE:
--   - Populated by: Category sync process
--   - Used for: Category dropdown menus, filtering, navigation
--   - Query patterns: Filter by Category, list distinct categories
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_categories
(
    `SeriesId` String,                            -- Series identifier (primary key)
    `Category` Nullable(String),                  -- Category name
    `Tags` Nullable(String),                      -- Associated tags (JSON)
    `Ticker` Nullable(String),                    -- Related ticker
    `Title` Nullable(String),                     -- Display title
    `Frequency` Nullable(String),                 -- Update frequency
    `JsonResponse` Nullable(String),              -- Full API response (JSON cache)
    `LastUpdate` DateTime                         -- Last sync timestamp
)
ENGINE = MergeTree()
ORDER BY SeriesId
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: orderbook_snapshots
-- =============================================================================
-- PURPOSE: Stores complete orderbook depth snapshots for high-priority markets.
--          Captures full bid/ask ladder at point in time.
-- 
-- USAGE:
--   - Populated by: SynchronizationService (orderbook sync)
--   - API Endpoint: POST /api/private/data-source/sync-orderbook
--   - Used for: Liquidity analysis, market depth visualization, spread tracking
--   - Query patterns: Filter by MarketId, time range on CapturedAt
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.orderbook_snapshots
(
    `Id` Int64,                                   -- Unique record identifier
    `MarketId` String,                            -- Market ticker ID
    `CapturedAt` DateTime,                        -- Snapshot capture timestamp
    `YesLevels` Nullable(String),                 -- Yes side price levels (JSON)
    `NoLevels` Nullable(String),                  -- No side price levels (JSON)
    `YesDollars` Nullable(String),                -- Yes levels in dollars (JSON)
    `NoDollars` Nullable(String),                 -- No levels in dollars (JSON)
    `BestYes` Nullable(Float64),                  -- Best Yes price
    `BestNo` Nullable(Float64),                   -- Best No price
    `Spread` Nullable(Float64),                   -- Bid-ask spread
    `TotalYesLiquidity` Float64,                  -- Total Yes side liquidity
    `TotalNoLiquidity` Float64,                   -- Total No side liquidity
    
    INDEX idx_orderbook_snapshots_market_id MarketId TYPE bloom_filter GRANULARITY 1,
    INDEX idx_orderbook_snapshots_captured_at CapturedAt TYPE minmax GRANULARITY 1
)
ENGINE = MergeTree()
ORDER BY (MarketId, CapturedAt)
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: orderbook_events
-- =============================================================================
-- PURPOSE: Stores individual orderbook change events (adds, removes, trades).
--          Used for real-time orderbook reconstruction and event analysis.
-- 
-- USAGE:
--   - Populated by: WebSocket streaming or event processing
--   - Used for: Real-time updates, order flow analysis, trade reconstruction
--   - Query patterns: Filter by MarketId, time range on EventTime
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.orderbook_events
(
    `Id` Int64,                                   -- Unique event identifier
    `MarketId` String,                            -- Market ticker ID
    `EventTime` DateTime,                         -- When event occurred
    `Side` String,                                -- 'Yes' or 'No'
    `Price` Float64,                              -- Price level affected
    `Size` Float64,                               -- Size/quantity change
    `EventType` String,                           -- Event type: 'add', 'remove', 'trade'
    
    INDEX idx_orderbook_events_market_id MarketId TYPE bloom_filter GRANULARITY 1,
    INDEX idx_orderbook_events_event_time EventTime TYPE minmax GRANULARITY 1
)
ENGINE = MergeTree()
ORDER BY (MarketId, EventTime)
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: TagsCategories
-- =============================================================================
-- PURPOSE: Stores the mapping between tags and categories for market organization.
--          Used for filtering and navigation in the UI.
-- 
-- USAGE:
--   - Populated by: SynchronizationService (tags/categories sync)
--   - API Endpoint: POST /api/private/data-source/sync-tags-categories
--   - Used for: Filter dropdowns, category organization, tag clouds
--   - Query patterns: Group by Category, list Tags per Category
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.TagsCategories
(
    `Id` Int64,                                   -- Unique record identifier
    `Category` String,                            -- Category name
    `Tag` String,                                 -- Tag name within category
    `LastUpdate` DateTime,                        -- Last sync timestamp
    `IsDeleted` UInt8 DEFAULT 0                   -- Soft delete flag
)
ENGINE = MergeTree()
ORDER BY (Category, Tag)
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: Users
-- =============================================================================
-- PURPOSE: Stores user account information for the Kalshi Signals application.
--          Integrated with Firebase Authentication.
-- 
-- USAGE:
--   - Populated by: User registration/sign-in flow
--   - API Endpoints: POST /api/auth/sign-in, PUT /api/users/profile
--   - Used for: Authentication, user preferences, email notifications
--   - Query patterns: Lookup by FirebaseId
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.Users
(
    `Id` UInt64 DEFAULT generateSerialID('users'), -- Auto-generated user ID
    `FirebaseId` String,                           -- Firebase UID (primary lookup key)
    `Username` Nullable(String),                   -- Display username
    `FirstName` Nullable(String),                  -- User's first name
    `LastName` Nullable(String),                   -- User's last name
    `Email` Nullable(String),                      -- Email address
    `IsComnEmailOn` UInt8 DEFAULT 0,               -- Boolean: marketing emails enabled
    `CreatedAt` DateTime,                          -- Account creation timestamp
    `UpdatedAt` DateTime                           -- Last profile update timestamp
)
ENGINE = MergeTree()
ORDER BY FirebaseId
SETTINGS index_granularity = 8192;


-- =============================================================================
-- RELATIONSHIPS AND DATA FLOW
-- =============================================================================
-- 
-- market_series (1) ──────────────── (N) market_events
--      │                                      │
--      │                                      │
--      └──────────── (N) market_snapshots ────┘
--                           │
--                           │
--                    market_highpriority
--                      (filter list)
--                           │
--                     ┌─────┴─────┐
--                     │           │
--              market_candlesticks   orderbook_snapshots
--              (OHLC data)          (depth data)
--
-- =============================================================================
-- 
-- DATA SYNC ENDPOINTS:
-- 
-- 1. /api/private/data-source/sync-market-snapshots
--    └── Fetches current market data → market_snapshots
-- 
-- 2. /api/private/data-source/sync-candlesticks
--    └── For markets in market_highpriority (FetchCandlesticks=1)
--    └── Fetches OHLC data → market_candlesticks
-- 
-- 3. /api/private/data-source/sync-orderbook
--    └── For markets in market_highpriority (FetchOrderbook=1)
--    └── Fetches orderbook depth → orderbook_snapshots
-- 
-- 4. /api/private/data-source/sync-series
--    └── Fetches series metadata → market_series
-- 
-- 5. /api/private/data-source/sync-events
--    └── Fetches event metadata → market_events
-- 
-- 6. /api/private/data-source/sync-tags-categories
--    └── Fetches tag/category mapping → TagsCategories
-- 
-- =============================================================================
