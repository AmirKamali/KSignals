-- =============================================================================
-- KALSHI SIGNALS DATABASE SCHEMA V2
-- ClickHouse Database Script
-- Version: 2.0
-- Last Updated: December 2024
-- =============================================================================
-- This script contains the complete schema for all tables in the Kalshi Signals
-- application. ClickHouse is used as the primary data store for market data,
-- orderbook snapshots, candlestick data, analytics features, and user information.
--
-- WHAT'S NEW IN V2:
--   - Added analytics_market_features table for ML/analytics pipeline
--   - Enhanced documentation for all columns
--   - Complete schema with all 11 tables
-- =============================================================================

-- Create database if not exists
CREATE DATABASE IF NOT EXISTS kalshi_signals;


-- =============================================================================
-- TABLE: market_snapshots
-- =============================================================================
-- PURPOSE: Stores point-in-time snapshots of market data from Kalshi API.
--          This is the primary table for market information including prices,
--          volume, and market metadata. Each row represents the state of a
--          market at a specific moment in time.
--
-- WHY THIS TABLE EXISTS:
--   - Primary source of truth for market prices and status
--   - Enables historical price analysis and backtesting
--   - Tracks market lifecycle from creation to settlement
--   - Provides data for trend detection and price alerts
--
-- USAGE:
--   - Populated by: SynchronizationService.InsertMarketSnapshotsAsync()
--   - API Endpoint: POST /api/private/data-source/sync-market-snapshots
--   - Consumers: Analytics pipeline, frontend market display, price alerts
--   - Query patterns: Filter by Ticker, Status, time ranges on GenerateDate
--   - Typical query: Get latest snapshot per ticker, time-series analysis
--
-- DATA RETENTION:
--   - Active markets: Keep all snapshots for trend analysis
--   - Settled markets: May be archived after 30 days
--   - Expected volume: ~100-500 snapshots per market per day
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_snapshots
(
    -- Primary identifier (auto-generated UUID)
    -- WHY: Ensures globally unique identifier for each snapshot
    -- USAGE: Used as primary key, referenced in foreign key relationships
    `MarketSnapshotID` UUID DEFAULT generateUUIDv4(),
    
    -- =========================================================================
    -- Market Identifiers
    -- These fields link the snapshot to market, series, and event hierarchies
    -- =========================================================================
    
    -- Unique market ticker (e.g., 'BEYONCEGENRE-30-AFA', 'KXBTC-24DEC31-B100000')
    -- WHY: Primary identifier from Kalshi API, human-readable
    -- USAGE: Main filter/join key, displayed in UI, used in API calls
    `Ticker` String,
    
    -- Parent series identifier (e.g., 'KXBEYONCEGENRE', 'KXBTC')
    -- WHY: Groups related markets together (all BTC price markets, etc.)
    -- USAGE: Category browsing, series-level analytics, grouping in UI
    `SeriesId` String,
    
    -- Associated event ticker (e.g., 'BEYONCEGENRE', 'BTC-24DEC31')
    -- WHY: Links to specific event within a series
    -- USAGE: Event-level filtering, grouping markets by event
    `EventTicker` String,
    
    -- =========================================================================
    -- Market Metadata
    -- Descriptive information about the market structure
    -- =========================================================================
    
    -- Type of market (binary, ranged, etc.)
    -- WHY: Different market types have different pricing/settlement mechanics
    -- USAGE: UI display logic, analytics stratification
    `MarketType` String,
    
    -- Display subtitle for Yes outcome (e.g., "Trump wins", "Over 100k")
    -- WHY: Human-readable description of what "Yes" means
    -- USAGE: Frontend display, search indexing
    `YesSubTitle` String,
    
    -- Display subtitle for No outcome
    -- WHY: Human-readable description of what "No" means
    -- USAGE: Frontend display, search indexing
    `NoSubTitle` String,
    
    -- =========================================================================
    -- Timestamps
    -- Critical time points in market lifecycle
    -- =========================================================================
    
    -- When market was created on Kalshi
    -- WHY: Track market age, sort by recency
    -- USAGE: "New markets" filtering, age-based analytics
    `CreatedTime` DateTime,
    
    -- When market opened for trading
    -- WHY: Trading may start after creation
    -- USAGE: Calculate active trading duration
    `OpenTime` DateTime,
    
    -- When market closes/closed for trading
    -- WHY: Critical for time-to-close calculations, trading cutoff
    -- USAGE: Countdown displays, TimeToCloseSeconds calculation
    `CloseTime` DateTime,
    
    -- Expected settlement time (may be null if not determined)
    -- WHY: When outcome is expected to be known
    -- USAGE: Settlement countdown, planning analytics
    `ExpectedExpirationTime` Nullable(DateTime),
    
    -- Latest possible expiration/settlement
    -- WHY: Absolute deadline for settlement
    -- USAGE: Risk calculations, worst-case planning
    `LatestExpirationTime` DateTime,
    
    -- Fee waiver end time (promotional periods)
    -- WHY: Kalshi may waive fees for new markets
    -- USAGE: Display fee status, trading cost calculations
    `FeeWaiverExpirationTime` Nullable(DateTime),
    
    -- =========================================================================
    -- Settlement Information
    -- Data related to market resolution
    -- =========================================================================
    
    -- Seconds until settlement (from API)
    -- WHY: Pre-calculated countdown from Kalshi
    -- USAGE: Direct display in UI without calculation
    `SettlementTimerSeconds` Int32,
    
    -- Final settlement value (cents) - null until settled
    -- WHY: The resolved outcome value (0 or 100 for binary)
    -- USAGE: P&L calculations, historical outcome analysis
    `SettlementValue` Nullable(Int32),
    
    -- Final settlement value (dollars) - formatted string
    -- WHY: Dollar-formatted for display
    -- USAGE: Direct UI display without conversion
    `SettlementValueDollars` Nullable(String),
    
    -- =========================================================================
    -- Market Status
    -- Current state and outcome information
    -- =========================================================================
    
    -- Market status: 'Active', 'Closed', 'Settled', 'Inactive'
    -- WHY: Determines if market is tradeable and data freshness
    -- USAGE: Filter active markets, status badges in UI
    `Status` String,
    
    -- Outcome result after settlement: 'yes', 'no', '' (empty if not settled)
    -- WHY: The final determined outcome
    -- USAGE: Historical analysis, P&L calculations
    `Result` String,
    
    -- Boolean: can market close early (1=true, 0=false)
    -- WHY: Some markets can resolve before scheduled close
    -- USAGE: Risk warnings, trading strategy considerations
    `CanCloseEarly` UInt8,
    
    -- Condition for early close (JSON or description)
    -- WHY: What triggers early closure
    -- USAGE: Display in market rules, risk assessment
    `EarlyCloseCondition` Nullable(String),
    
    -- =========================================================================
    -- Current Pricing (in cents and dollars)
    -- Core trading prices at snapshot time
    -- =========================================================================
    
    -- Price unit type from API response
    -- WHY: Indicates how prices are denominated
    -- USAGE: Price parsing logic
    `ResponsePriceUnits` String,
    
    -- Best Yes bid price (cents, 0-100)
    -- WHY: Highest price someone will pay for Yes
    -- USAGE: Trading decisions, spread calculations, analytics
    `YesBid` Decimal(18, 8),
    
    -- Best Yes bid price (dollars formatted, e.g., "0.45")
    -- WHY: Pre-formatted for display
    -- USAGE: Direct UI display
    `YesBidDollars` String,
    
    -- Best Yes ask price (cents, 0-100)
    -- WHY: Lowest price someone will sell Yes for
    -- USAGE: Trading decisions, spread calculations
    `YesAsk` Decimal(18, 8),
    
    -- Best Yes ask price (dollars)
    `YesAskDollars` String,
    
    -- Best No bid price (cents)
    -- WHY: Highest price for No contracts (inverse of Yes)
    -- USAGE: Arbitrage detection, No-side trading
    `NoBid` Decimal(18, 8),
    
    `NoBidDollars` String,
    
    -- Best No ask price (cents)
    `NoAsk` Decimal(18, 8),
    
    `NoAskDollars` String,
    
    -- Last traded price (cents)
    -- WHY: Most recent execution price
    -- USAGE: Current price display, change calculations
    `LastPrice` Decimal(18, 8),
    
    `LastPriceDollars` String,
    
    -- =========================================================================
    -- Previous Prices (for change calculation)
    -- Prices from previous snapshot for delta calculations
    -- =========================================================================
    
    -- Previous Yes bid (cents)
    -- WHY: Calculate price movement, display change indicators
    -- USAGE: "Up 5%" badges, trend arrows
    `PreviousYesBid` Int32,
    `PreviousYesBidDollars` String,
    
    -- Previous Yes ask (cents)
    `PreviousYesAsk` Int32,
    `PreviousYesAskDollars` String,
    
    -- Previous last traded price (cents)
    `PreviousPrice` Int32,
    `PreviousPriceDollars` String,
    
    -- =========================================================================
    -- Volume and Liquidity
    -- Trading activity and market depth indicators
    -- =========================================================================
    
    -- Total contracts ever traded
    -- WHY: Lifetime volume indicates market maturity/interest
    -- USAGE: Sorting by popularity, liquidity assessment
    `Volume` Int32,
    
    -- Contracts traded in last 24 hours
    -- WHY: Recent activity indicator
    -- USAGE: "Hot markets" filtering, activity badges
    `Volume24h` Int32,
    
    -- Open contract positions (outstanding contracts)
    -- WHY: Indicates market depth and commitment
    -- USAGE: Liquidity scoring, market health indicators
    `OpenInterest` Int32,
    
    -- Total notional value (cents) = volume * price
    -- WHY: Dollar-weighted activity measure
    -- USAGE: Size-weighted rankings, institutional interest proxy
    `NotionalValue` Int32,
    `NotionalValueDollars` String,
    
    -- Available liquidity (cents) - total value available to trade
    -- WHY: How much can be traded without moving price
    -- USAGE: Large order feasibility, slippage estimation
    `Liquidity` Int32,
    `LiquidityDollars` String,
    
    -- =========================================================================
    -- Strike/Price Structure
    -- Market-specific pricing parameters
    -- =========================================================================
    
    -- Value at expiration (description or value)
    -- WHY: What the market settles to
    -- USAGE: Rules display, settlement logic
    `ExpirationValue` String,
    
    -- Minimum price increment (typically 1 cent)
    -- WHY: Defines valid price levels
    -- USAGE: Price validation, orderbook display
    `TickSize` Int32,
    
    -- Type of strike (for ranged markets)
    -- WHY: Categorizes strike structure
    -- USAGE: Market type-specific logic
    `StrikeType` Nullable(String),
    
    -- Floor strike price (for ranged markets)
    -- WHY: Lower bound of range
    -- USAGE: Ranged market calculations
    `FloorStrike` Nullable(Float64),
    
    -- Cap strike price (for ranged markets)
    -- WHY: Upper bound of range
    -- USAGE: Ranged market calculations
    `CapStrike` Nullable(Float64),
    
    -- Functional strike definition
    -- WHY: Mathematical definition of strike
    -- USAGE: Advanced analytics
    `FunctionalStrike` Nullable(String),
    
    -- Custom strike (JSON string for complex strikes)
    -- WHY: Flexible strike definitions
    -- USAGE: Complex market types
    `CustomStrike` Nullable(String),
    
    -- =========================================================================
    -- Rules and Metadata
    -- Market rules and auxiliary information
    -- =========================================================================
    
    -- Primary market rules (settlement criteria)
    -- WHY: Defines how market settles
    -- USAGE: Rules display, contract understanding
    `RulesPrimary` String,
    
    -- Secondary market rules (additional terms)
    `RulesSecondary` String,
    
    -- MVE (Multi-Variable Event) collection ticker
    -- WHY: Links to parent MVE structure
    -- USAGE: MVE market grouping
    `MveCollectionTicker` Nullable(String),
    
    -- MVE selected legs (JSON)
    -- WHY: Which legs of MVE are selected
    -- USAGE: MVE position tracking
    `MveSelectedLegs` Nullable(String),
    
    -- Primary participant identifier
    -- WHY: Main entity in market (e.g., candidate name)
    -- USAGE: Search, filtering by participant
    `PrimaryParticipantKey` Nullable(String),
    
    -- Price level structure type
    -- WHY: How price levels are organized
    -- USAGE: Orderbook rendering
    `PriceLevelStructure` String,
    
    -- Price ranges (JSON for ranged markets)
    -- WHY: Defines valid price ranges
    -- USAGE: Ranged market display
    `PriceRanges` Nullable(String),
    
    -- =========================================================================
    -- Record Metadata
    -- =========================================================================
    
    -- When this snapshot was captured
    -- WHY: Primary time dimension for time-series queries
    -- USAGE: ORDER BY, time-range filters, data freshness
    `GenerateDate` DateTime
)
ENGINE = MergeTree()
ORDER BY (Ticker, SeriesId, GenerateDate)
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: market_highpriority
-- =============================================================================
-- PURPOSE: Stores list of high-priority markets for enhanced data collection.
--          Markets in this table get additional data fetching (candlesticks,
--          orderbook depth, analytics) beyond standard snapshots.
--
-- WHY THIS TABLE EXISTS:
--   - Not all markets need detailed data (would be expensive)
--   - Allows selective enhancement for interesting markets
--   - Controls analytics pipeline processing
--   - Enables tiered data collection strategy
--
-- USAGE:
--   - Managed by: Admin API, manual insertion, or automated rules
--   - Used by: SynchronizeCandlesticksConsumer, SynchronizeOrderbookConsumer,
--              ProcessMarketAnalyticsConsumer
--   - Controls: Which markets get candlestick, orderbook, and analytics data
--   - Query patterns: Filter by Fetch* and ProcessAnalytics* flags
--
-- DATA VOLUME:
--   - Typically 10-100 markets at any time
--   - Markets added/removed based on activity, user interest
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_highpriority
(
    -- Market ticker ID (primary key)
    -- WHY: Links to market_snapshots.Ticker
    -- USAGE: Join key, unique identifier for enhanced markets
    `TickerId` String,
    
    -- Priority level (higher = more important)
    -- WHY: Determines processing order and frequency
    -- USAGE: Sort by importance, resource allocation
    `Priority` Int32,
    
    -- Last modification timestamp
    -- WHY: Used by ReplacingMergeTree for deduplication
    -- USAGE: Keep latest version when multiple inserts
    `LastUpdate` DateTime,
    
    -- Boolean: fetch candlestick data (1=true, 0=false)
    -- WHY: Controls OHLC data collection
    -- USAGE: SynchronizeCandlesticksConsumer checks this flag
    `FetchCandlesticks` UInt8 DEFAULT 1,
    
    -- Boolean: fetch orderbook data
    -- WHY: Controls depth data collection
    -- USAGE: SynchronizeOrderbookConsumer checks this flag
    `FetchOrderbook` UInt8 DEFAULT 1,
    
    -- Boolean: process L1 analytics (basic features)
    -- WHY: Controls basic feature extraction (prices, spreads, time)
    -- USAGE: ProcessMarketAnalyticsConsumer checks this flag
    -- FEATURES: YesBidProb, YesAskProb, BidAskSpread, TimeToClose, etc.
    `ProcessAnalyticsL1` UInt8 DEFAULT 0,
    
    -- Boolean: process L2 analytics (volatility/returns)
    -- WHY: Controls historical analysis features
    -- USAGE: ProcessMarketAnalyticsConsumer checks this flag
    -- FEATURES: Return1h, Return24h, Volatility1h, Volatility24h
    `ProcessAnalyticsL2` UInt8 DEFAULT 0,
    
    -- Boolean: process L3 analytics (advanced metrics)
    -- WHY: Controls orderbook-based features
    -- USAGE: ProcessMarketAnalyticsConsumer checks this flag
    -- FEATURES: OrderbookImbalance, TotalLiquidity, TopOfBook
    `ProcessAnalyticsL3` UInt8 DEFAULT 0,
    
    INDEX idx_market_highpriority_priority Priority TYPE minmax GRANULARITY 1
)
ENGINE = ReplacingMergeTree(LastUpdate)
ORDER BY TickerId
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: market_candlesticks
-- =============================================================================
-- PURPOSE: Stores OHLC (Open-High-Low-Close) candlestick data for markets.
--          Used for charting, technical analysis, and price trend detection.
--
-- WHY THIS TABLE EXISTS:
--   - Candlestick data is aggregated time-series data
--   - More efficient than querying raw snapshots for charts
--   - Standard format for technical analysis tools
--   - Supports multiple time intervals (1min, 1hr, 1day)
--
-- USAGE:
--   - Populated by: SynchronizationService.SynchronizeCandlesticksAsync()
--   - API Endpoint: POST /api/private/data-source/sync-candlesticks
--   - Data source: Kalshi API GetMarketCandlesticksAsync()
--   - Consumers: Charting frontend, technical analysis, L2 analytics
--   - Query patterns: Filter by Ticker, PeriodInterval, time range
--   - Intervals: 1 minute (1), 1 hour (60), 1 day (1440)
--
-- DATA VOLUME:
--   - ~1440 candles/day/market for 1-minute interval
--   - ~24 candles/day/market for 1-hour interval
--   - Only collected for markets in market_highpriority
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_candlesticks
(
    -- Auto-generated UUID
    -- WHY: Unique identifier for each candlestick record
    -- USAGE: Primary key, deduplication
    `Id` UUID DEFAULT generateUUIDv4(),
    
    -- Market ticker ID
    -- WHY: Links to market_snapshots.Ticker
    -- USAGE: Filter/join key for market-specific queries
    `Ticker` String,
    
    -- Parent series ticker
    -- WHY: Groups candlesticks by series for series-level charts
    -- USAGE: Series-aggregated analytics
    `SeriesTicker` String,
    
    -- Candle interval in minutes (1, 60, 1440)
    -- WHY: Supports multiple timeframes
    -- USAGE: Filter by timeframe (1m, 1h, 1d charts)
    -- VALUES: 1 = 1 minute, 60 = 1 hour, 1440 = 1 day
    `PeriodInterval` Int32,
    
    -- End period Unix timestamp (seconds since epoch)
    -- WHY: Raw timestamp for precise time calculations
    -- USAGE: Timestamp math, API compatibility
    `EndPeriodTs` Int64,
    
    -- End period as DateTime
    -- WHY: Human-readable and ClickHouse-optimized time queries
    -- USAGE: Time-range filters, ORDER BY
    `EndPeriodTime` DateTime,
    
    -- =========================================================================
    -- Yes Bid OHLC (in cents)
    -- Best bid price at each point in the candle period
    -- =========================================================================
    
    -- Opening Yes bid price (cents) at period start
    -- WHY: First bid price in period
    -- USAGE: Gap analysis, trend identification
    `YesBidOpen` Int32,
    
    -- Lowest Yes bid price (cents) during period
    -- WHY: Period low for bid side
    -- USAGE: Support level identification, volatility
    `YesBidLow` Int32,
    
    -- Highest Yes bid price (cents) during period
    -- WHY: Period high for bid side
    -- USAGE: Resistance level identification
    `YesBidHigh` Int32,
    
    -- Closing Yes bid price (cents) at period end
    -- WHY: Final bid price, most "current" at candle close
    -- USAGE: Primary price for charts, trend analysis
    `YesBidClose` Int32,
    
    -- =========================================================================
    -- Yes Bid OHLC (in dollars)
    -- Dollar-formatted versions for direct display
    -- =========================================================================
    `YesBidOpenDollars` String,
    `YesBidLowDollars` String,
    `YesBidHighDollars` String,
    `YesBidCloseDollars` String,
    
    -- =========================================================================
    -- Yes Ask OHLC (in cents)
    -- Best ask price at each point in the candle period
    -- =========================================================================
    
    -- Opening Yes ask price at period start
    `YesAskOpen` Int32,
    
    -- Lowest Yes ask price during period
    `YesAskLow` Int32,
    
    -- Highest Yes ask price during period
    `YesAskHigh` Int32,
    
    -- Closing Yes ask price at period end
    `YesAskClose` Int32,
    
    -- =========================================================================
    -- Yes Ask OHLC (in dollars)
    -- =========================================================================
    `YesAskOpenDollars` String,
    `YesAskLowDollars` String,
    `YesAskHighDollars` String,
    `YesAskCloseDollars` String,
    
    -- =========================================================================
    -- Price OHLC (nullable - no trades may occur during period)
    -- Actual trade prices (not bid/ask quotes)
    -- =========================================================================
    
    -- Opening trade price (cents)
    -- WHY: Nullable because no trades may occur in period
    -- USAGE: Actual execution price tracking
    `PriceOpen` Nullable(Int32),
    
    -- Lowest trade price
    `PriceLow` Nullable(Int32),
    
    -- Highest trade price
    `PriceHigh` Nullable(Int32),
    
    -- Closing trade price (last trade in period)
    -- WHY: Most relevant trade price for period
    -- USAGE: Primary "price" for many calculations
    `PriceClose` Nullable(Int32),
    
    -- Mean/average trade price during period
    -- WHY: Volume-weighted or simple average
    -- USAGE: Fair value estimation
    `PriceMean` Nullable(Int32),
    
    -- Previous period's close price
    -- WHY: Enables period-over-period change calculation
    -- USAGE: Return calculations, gap detection
    `PricePrevious` Nullable(Int32),
    
    -- =========================================================================
    -- Price OHLC (in dollars)
    -- =========================================================================
    `PriceOpenDollars` Nullable(String),
    `PriceLowDollars` Nullable(String),
    `PriceHighDollars` Nullable(String),
    `PriceCloseDollars` Nullable(String),
    `PriceMeanDollars` Nullable(String),
    `PricePreviousDollars` Nullable(String),
    
    -- =========================================================================
    -- Volume and Interest
    -- Trading activity within the period
    -- =========================================================================
    
    -- Contracts traded during this period
    -- WHY: Activity measure for the period
    -- USAGE: Volume bars in charts, activity filtering
    `Volume` Int64,
    
    -- Open interest at period end
    -- WHY: Snapshot of outstanding contracts
    -- USAGE: OI charts, market commitment tracking
    `OpenInterest` Int64,
    
    -- =========================================================================
    -- Record Metadata
    -- =========================================================================
    
    -- When this data was fetched from API
    -- WHY: Track data freshness, debug sync issues
    -- USAGE: Data quality monitoring
    `FetchedAt` DateTime,
    
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
--          (e.g., all BTC price markets for different dates/strikes).
--
-- WHY THIS TABLE EXISTS:
--   - Series is a core Kalshi concept for market organization
--   - Groups markets by theme (all election markets, all crypto markets)
--   - Contains category and tag information for filtering
--   - Caches series metadata to avoid repeated API calls
--
-- USAGE:
--   - Populated by: SynchronizationService (series sync)
--   - API Endpoint: POST /api/private/data-source/sync-series
--   - Consumers: Category browsing, filtering, market grouping
--   - Query patterns: Filter by Category, Tags, list all series
--
-- DATA VOLUME:
--   - ~500-2000 series total
--   - Updated infrequently (new series created weekly/monthly)
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_series
(
    -- Series ticker (primary key, e.g., 'KXBTC', 'KXELECTION')
    -- WHY: Unique identifier from Kalshi
    -- USAGE: Primary key, join to market_snapshots.SeriesId
    `Ticker` String,
    
    -- Update frequency (e.g., 'daily', 'weekly', 'one-time')
    -- WHY: How often new markets are created in series
    -- USAGE: Expectation setting, scheduling
    `Frequency` String,
    
    -- Human-readable title (e.g., "Bitcoin Price Markets")
    -- WHY: Display name for series
    -- USAGE: UI display, search
    `Title` String,
    
    -- Category classification (e.g., 'Crypto', 'Politics', 'Economics')
    -- WHY: Top-level categorization
    -- USAGE: Category dropdowns, filtering, analytics by category
    `Category` String,
    
    -- Tags (JSON array as string, e.g., '["bitcoin", "crypto", "price"]')
    -- WHY: Fine-grained tagging for search/filter
    -- USAGE: Tag clouds, multi-tag filtering
    `Tags` Nullable(String),
    
    -- Settlement data sources (JSON)
    -- WHY: Where settlement data comes from (Reuters, official sources)
    -- USAGE: Transparency, rules display
    `SettlementSources` Nullable(String),
    
    -- Contract details URL
    -- WHY: Link to full contract specification
    -- USAGE: "Learn more" links
    `ContractUrl` Nullable(String),
    
    -- Terms and conditions URL
    -- WHY: Legal terms for the series
    -- USAGE: Compliance, legal links
    `ContractTermsUrl` Nullable(String),
    
    -- Additional metadata (JSON)
    -- WHY: Flexible field for series-specific data
    -- USAGE: Custom series information
    `ProductMetadata` Nullable(String),
    
    -- Fee structure type (e.g., 'standard', 'reduced')
    -- WHY: Different series may have different fee structures
    -- USAGE: Trading cost calculations
    `FeeType` String,
    
    -- Fee multiplier value
    -- WHY: Numeric fee factor
    -- USAGE: Precise fee calculations
    `FeeMultiplier` Float64,
    
    -- Trading restrictions
    -- WHY: Any additional trading limitations
    -- USAGE: Compliance, eligibility checking
    `AdditionalProhibitions` Nullable(String),
    
    -- Last sync timestamp
    -- WHY: Used by ReplacingMergeTree for deduplication
    -- USAGE: Keep latest version, track data freshness
    `LastUpdate` DateTime,
    
    -- Soft delete flag (1=deleted, 0=active)
    -- WHY: Mark removed series without deleting history
    -- USAGE: Filter WHERE IsDeleted = 0
    `IsDeleted` UInt8 DEFAULT 0,
    
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
--          occurrences that markets are built around (e.g., "BTC price on Dec 31").
--          Multiple markets can exist within one event (different strike prices).
--
-- WHY THIS TABLE EXISTS:
--   - Events are the next level down from series in Kalshi hierarchy
--   - Groups related markets within a series
--   - Contains event-specific metadata (strike date, etc.)
--   - Enables event-level browsing and analytics
--
-- USAGE:
--   - Populated by: SynchronizationService (events sync)
--   - API Endpoint: POST /api/private/data-source/sync-events
--   - Consumers: Event browsing, market grouping, category filtering
--   - Query patterns: Filter by SeriesTicker, Category, StrikeDate
--
-- HIERARCHY:
--   Series (KXBTC) → Event (BTC-24DEC31) → Markets (KXBTC-24DEC31-B100000, etc.)
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_events
(
    -- Event ticker (primary key)
    -- WHY: Unique identifier from Kalshi
    -- USAGE: Primary key, join to market_snapshots.EventTicker
    `EventTicker` String,
    
    -- Parent series ticker
    -- WHY: Links to market_series.Ticker
    -- USAGE: Series-level grouping, join key
    `SeriesTicker` String,
    
    -- Event subtitle
    -- WHY: Brief description of the event
    -- USAGE: UI display below title
    `SubTitle` String,
    
    -- Event title
    -- WHY: Main display name for event
    -- USAGE: Primary UI display, search
    `Title` String,
    
    -- How collateral is returned (e.g., 'immediate', 'after_settlement')
    -- WHY: Affects capital efficiency
    -- USAGE: Trading strategy, capital management
    `CollateralReturnType` String,
    
    -- Boolean: outcomes mutually exclusive (1=true)
    -- WHY: If true, only one outcome can win
    -- USAGE: Portfolio risk calculations, UI grouping
    `MutuallyExclusive` UInt8,
    
    -- Event category (may differ from series category)
    -- WHY: Event-level categorization
    -- USAGE: Filtering, analytics
    `Category` String,
    
    -- Strike/settlement date
    -- WHY: When event resolves
    -- USAGE: Date-based filtering, countdown displays
    `StrikeDate` Nullable(DateTime),
    
    -- Strike period description (e.g., "End of Q4 2024")
    -- WHY: Human-readable period description
    -- USAGE: Display when exact date unknown
    `StrikePeriod` Nullable(String),
    
    -- Boolean: available via brokers (1=true)
    -- WHY: Some events only available on Kalshi directly
    -- USAGE: Integration eligibility
    `AvailableOnBrokers` UInt8,
    
    -- Additional metadata (JSON)
    -- WHY: Flexible field for event-specific data
    -- USAGE: Custom event information
    `ProductMetadata` Nullable(String),
    
    -- Last sync timestamp
    `LastUpdate` DateTime,
    
    -- Soft delete flag
    `IsDeleted` UInt8 DEFAULT 0,
    
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
-- WHY THIS TABLE EXISTS:
--   - Denormalized view of category relationships
--   - Enables fast category filtering without joins
--   - Caches full API responses for quick access
--   - Optimizes category dropdown and navigation
--
-- USAGE:
--   - Populated by: Category sync process
--   - Consumers: Category dropdown menus, filtering, navigation
--   - Query patterns: Filter by Category, list distinct categories
--
-- NOTE:
--   - This table may have overlap with market_series
--   - Used primarily for UI navigation optimization
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.market_categories
(
    -- Series identifier (primary key)
    -- WHY: Links to market_series
    -- USAGE: Primary key, relationship key
    `SeriesId` String,
    
    -- Category name
    -- WHY: Denormalized from series for fast filtering
    -- USAGE: Category dropdown, WHERE clause
    `Category` Nullable(String),
    
    -- Associated tags (JSON)
    -- WHY: Denormalized tags for filtering
    -- USAGE: Tag-based filtering
    `Tags` Nullable(String),
    
    -- Related ticker
    -- WHY: Primary ticker for the series
    -- USAGE: Display, linking
    `Ticker` Nullable(String),
    
    -- Display title
    -- WHY: Series title for display
    -- USAGE: UI display
    `Title` Nullable(String),
    
    -- Update frequency
    -- WHY: From series metadata
    -- USAGE: Display, filtering
    `Frequency` Nullable(String),
    
    -- Full API response (JSON cache)
    -- WHY: Cache complete response to avoid re-fetching
    -- USAGE: Hydrate complex objects without API call
    `JsonResponse` Nullable(String),
    
    -- Last sync timestamp
    `LastUpdate` DateTime
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
-- WHY THIS TABLE EXISTS:
--   - Orderbook depth shows liquidity at all price levels
--   - Enables market depth visualization
--   - Required for liquidity analysis and slippage estimation
--   - Supports L3 analytics (orderbook imbalance, etc.)
--
-- USAGE:
--   - Populated by: SynchronizationService (orderbook sync)
--   - API Endpoint: POST /api/private/data-source/sync-orderbook
--   - Consumers: Depth charts, liquidity analysis, L3 analytics
--   - Query patterns: Filter by MarketId, time range on CapturedAt
--   - Only collected for markets with FetchOrderbook=1 in market_highpriority
--
-- DATA VOLUME:
--   - ~1 snapshot per minute per high-priority market
--   - JSON fields can be large (20+ price levels)
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.orderbook_snapshots
(
    -- Auto-generated UUID
    -- WHY: Unique identifier for each snapshot
    -- USAGE: Primary key
    `Id` UUID DEFAULT generateUUIDv4(),
    
    -- Market ticker ID
    -- WHY: Links to market_snapshots.Ticker
    -- USAGE: Filter/join key
    `MarketId` String,
    
    -- Snapshot capture timestamp
    -- WHY: When orderbook state was captured
    -- USAGE: Time-series queries, ORDER BY
    `CapturedAt` DateTime,
    
    -- Yes side price levels (JSON)
    -- WHY: Full depth on Yes side
    -- FORMAT: '[[price1, size1], [price2, size2], ...]' sorted by price desc
    -- USAGE: Depth chart rendering, liquidity analysis
    `YesLevels` Nullable(String),
    
    -- No side price levels (JSON)
    -- WHY: Full depth on No side
    -- FORMAT: Same as YesLevels
    -- USAGE: Depth chart rendering
    `NoLevels` Nullable(String),
    
    -- Yes levels in dollars (JSON)
    -- WHY: Pre-formatted for display
    -- USAGE: Direct UI rendering
    `YesDollars` Nullable(String),
    
    -- No levels in dollars (JSON)
    `NoDollars` Nullable(String),
    
    -- Best Yes price (top of book)
    -- WHY: Extracted for quick access without JSON parsing
    -- USAGE: Quick lookups, analytics
    `BestYes` Nullable(Float64),
    
    -- Best No price (top of book)
    `BestNo` Nullable(Float64),
    
    -- Bid-ask spread (calculated: 100 - BestYes - BestNo or similar)
    -- WHY: Key liquidity metric
    -- USAGE: Spread tracking, liquidity scoring
    `Spread` Nullable(Float64),
    
    -- Total Yes side liquidity (sum of all sizes)
    -- WHY: Aggregate liquidity measure
    -- USAGE: L3 analytics, liquidity comparison
    `TotalYesLiquidity` Float64,
    
    -- Total No side liquidity
    `TotalNoLiquidity` Float64,
    
    INDEX idx_orderbook_snapshots_market_id MarketId TYPE bloom_filter GRANULARITY 1,
    INDEX idx_orderbook_snapshots_captured_at CapturedAt TYPE minmax GRANULARITY 1
)
ENGINE = MergeTree()
ORDER BY (MarketId, CapturedAt)
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: orderbook_events
-- =============================================================================
-- PURPOSE: Stores individual orderbook change events (adds, removes, updates).
--          Used for real-time orderbook reconstruction and event analysis.
--
-- WHY THIS TABLE EXISTS:
--   - Captures granular orderbook changes between snapshots
--   - Enables orderbook replay/reconstruction
--   - Supports order flow analysis (seeing adds/removes over time)
--   - More efficient than storing full snapshots for each change
--
-- USAGE:
--   - Populated by: Event processing comparing consecutive snapshots
--   - Consumers: Order flow analysis, real-time updates, ML features
--   - Query patterns: Filter by MarketId, time range on EventTime
--   - Replay: Apply events in order to reconstruct orderbook state
--
-- EVENT TYPES:
--   - 'add': New price level appears (size goes from 0 to > 0)
--   - 'update': Price level size changes (remains > 0)
--   - 'remove': Price level disappears (size goes to 0)
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.orderbook_events
(
    -- Auto-generated UUID
    -- WHY: Unique identifier for each event
    -- USAGE: Primary key
    `Id` UUID DEFAULT generateUUIDv4(),
    
    -- Market ticker ID
    -- WHY: Links to market_snapshots.Ticker
    -- USAGE: Filter/join key
    `MarketId` String,
    
    -- When event occurred
    -- WHY: Event timestamp for ordering and filtering
    -- USAGE: ORDER BY, time-range filters
    `EventTime` DateTime,
    
    -- Side of orderbook: 'Yes' or 'No'
    -- WHY: Identifies which side of book changed
    -- USAGE: Separate Yes/No event analysis
    `Side` String,
    
    -- Price level affected
    -- WHY: Which price level changed
    -- USAGE: Reconstruct book[side, price] = size
    `Price` Float64,
    
    -- Size at this price AFTER the event
    -- WHY: Absolute size, not delta, for simpler replay
    -- USAGE: Set book[side, price] = size (or remove if size=0)
    -- NOTE: For 'remove' events, this is 0
    `Size` Float64,
    
    -- Event type: 'add', 'update', 'remove'
    -- WHY: Categorizes the type of change
    -- USAGE: Event type filtering, analysis by type
    `EventType` String,
    
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
-- WHY THIS TABLE EXISTS:
--   - Provides normalized tag-to-category relationships
--   - Enables "tags within category" filtering
--   - Supports tag cloud generation per category
--   - Independent of series/event tables for flexibility
--
-- USAGE:
--   - Populated by: SynchronizationService (tags/categories sync)
--   - API Endpoint: POST /api/private/data-source/sync-tags-categories
--   - Consumers: Filter dropdowns, category organization, tag clouds
--   - Query patterns: Group by Category, list Tags per Category
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.TagsCategories
(
    -- Unique record identifier
    -- WHY: Simple primary key (not UUID for efficiency)
    -- USAGE: Primary key, deduplication
    `Id` Int64,
    
    -- Category name
    -- WHY: Groups tags by category
    -- USAGE: First-level filter, GROUP BY
    `Category` String,
    
    -- Tag name within category
    -- WHY: Specific tag value
    -- USAGE: Second-level filter, display
    `Tag` String,
    
    -- Last sync timestamp
    -- WHY: Track data freshness
    -- USAGE: Debugging, freshness checks
    `LastUpdate` DateTime,
    
    -- Soft delete flag
    -- WHY: Mark removed tags without deleting
    -- USAGE: Filter WHERE IsDeleted = 0
    `IsDeleted` UInt8 DEFAULT 0
)
ENGINE = MergeTree()
ORDER BY (Category, Tag)
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: Users
-- =============================================================================
-- PURPOSE: Stores user account information for the Kalshi Signals application.
--          Integrated with Firebase Authentication for identity management.
--
-- WHY THIS TABLE EXISTS:
--   - Firebase handles authentication, but we need app-specific user data
--   - Stores user preferences and profile information
--   - Links Firebase UID to application user ID
--   - Supports email preferences and profile customization
--
-- USAGE:
--   - Populated by: User registration/sign-in flow
--   - API Endpoints: POST /api/auth/sign-in, PUT /api/users/profile
--   - Consumers: Authentication, user preferences, email notifications
--   - Query patterns: Lookup by FirebaseId (unique index)
--
-- SECURITY:
--   - FirebaseId is the link to Firebase Authentication
--   - Never store passwords (handled by Firebase)
--   - Email may be used for notifications based on IsComnEmailOn
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.Users
(
    -- Auto-generated user ID
    -- WHY: Application-internal user identifier
    -- USAGE: Foreign key in future user-related tables
    `Id` UInt64 DEFAULT generateSerialID('users'),
    
    -- Firebase UID (primary lookup key)
    -- WHY: Links to Firebase Authentication
    -- USAGE: Lookup user after Firebase auth, unique constraint
    `FirebaseId` String,
    
    -- Display username (optional)
    -- WHY: User-chosen display name
    -- USAGE: Display in UI, comments, social features
    `Username` Nullable(String),
    
    -- User's first name
    -- WHY: Personalization
    -- USAGE: "Hello, [FirstName]" greetings
    `FirstName` Nullable(String),
    
    -- User's last name
    -- WHY: Full name for formal communications
    -- USAGE: Email templates, account display
    `LastName` Nullable(String),
    
    -- Email address
    -- WHY: Contact method (may duplicate Firebase email)
    -- USAGE: Notifications, password reset fallback
    `Email` Nullable(String),
    
    -- Boolean: marketing/communication emails enabled (1=true)
    -- WHY: Email preference setting
    -- USAGE: Check before sending promotional emails
    `IsComnEmailOn` UInt8 DEFAULT 0,
    
    -- Account creation timestamp
    -- WHY: Track user tenure
    -- USAGE: "Member since" display, analytics
    `CreatedAt` DateTime,
    
    -- Last profile update timestamp
    -- WHY: Track profile modifications
    -- USAGE: Audit, caching invalidation
    `UpdatedAt` DateTime
)
ENGINE = MergeTree()
ORDER BY FirebaseId
SETTINGS index_granularity = 8192;


-- =============================================================================
-- TABLE: analytics_market_features
-- =============================================================================
-- PURPOSE: Stores computed market features for analytics and ML purposes.
--          This is the primary feature store for machine learning models and
--          advanced analytics dashboards.
--
-- WHY THIS TABLE EXISTS:
--   - Pre-computed features avoid expensive real-time calculations
--   - Standardized feature format for ML model training/inference
--   - Enables historical feature analysis and backtesting
--   - Separates feature engineering from model training
--   - Supports multi-level feature computation (L1/L2/L3)
--
-- USAGE:
--   - Populated by: ProcessMarketAnalyticsConsumer (via AnalyticsService)
--   - Trigger: Markets in market_highpriority with ProcessAnalytics* flags
--   - Consumers: ML pipelines, analytics dashboards, signal generation
--   - Query patterns: Filter by Ticker, time range on FeatureTime
--   - Feature levels:
--       L1: Basic (prices, spreads, time) - fast, always available
--       L2: Historical (returns, volatility) - requires history
--       L3: Advanced (orderbook imbalance) - requires orderbook data
--
-- DATA VOLUME:
--   - 1 feature row per analytics run per market
--   - Typically 1-4 rows per hour per high-priority market
--   - May grow to millions of rows for historical analysis
-- =============================================================================
CREATE TABLE IF NOT EXISTS kalshi_signals.analytics_market_features
(
    -- Auto-generated feature ID (via ClickHouse generateUUIDv4)
    -- WHY: Unique identifier for each feature computation
    -- USAGE: Primary key, deduplication
    `FeatureId` UUID DEFAULT generateUUIDv4(),
    
    -- =========================================================================
    -- Market Identifiers
    -- Links to market hierarchy for joining with other tables
    -- =========================================================================
    
    -- Market ticker (joins to market_snapshots.Ticker)
    -- WHY: Primary market identifier
    -- USAGE: Join key, filtering, grouping
    `Ticker` String,
    
    -- Series ID (joins to market_series.Ticker)
    -- WHY: Series-level aggregation and filtering
    -- USAGE: Series-level analytics, category inference
    `SeriesId` String,
    
    -- Event ticker (joins to market_events.EventTicker)
    -- WHY: Event-level grouping
    -- USAGE: Event-level analytics
    `EventTicker` String,
    
    -- =========================================================================
    -- Time Context
    -- When features were computed and time-based features
    -- =========================================================================
    
    -- When features were generated (usually = snapshot GenerateDate)
    -- WHY: Primary time dimension for features
    -- USAGE: Time-series queries, feature versioning, ORDER BY
    `FeatureTime` DateTime,
    
    -- Seconds until market closes (CloseTime - FeatureTime)
    -- WHY: Time decay is critical for prediction models
    -- USAGE: ML feature, urgency scoring, time-weighted strategies
    -- CALCULATION: (CloseTime - FeatureTime).TotalSeconds
    `TimeToCloseSeconds` Int64,
    
    -- Seconds until expected expiration (if available)
    -- WHY: Settlement timing affects pricing
    -- USAGE: ML feature, settlement timing analysis
    -- NOTE: 0 if ExpectedExpirationTime is null
    `TimeToExpirationSeconds` Int64,
    
    -- =========================================================================
    -- Prices in Probability Space (0-1)
    -- Normalized prices for ML and cross-market comparison
    -- =========================================================================
    
    -- YesBid / 100.0 (probability that Yes wins, bid side)
    -- WHY: Normalized to 0-1 for ML models
    -- USAGE: Primary price feature, probability estimation
    -- CALCULATION: snapshot.YesBid / 100.0
    `YesBidProb` Float64,
    
    -- YesAsk / 100.0 (probability that Yes wins, ask side)
    -- WHY: Best offer price as probability
    -- USAGE: Spread calculations, fair value estimation
    `YesAskProb` Float64,
    
    -- NoBid / 100.0 (probability that No wins, bid side)
    -- WHY: Inverse market price
    -- USAGE: Arbitrage detection, cross-market analysis
    `NoBidProb` Float64,
    
    -- NoAsk / 100.0
    `NoAskProb` Float64,
    
    -- (YesBidProb + YesAskProb) / 2 (midpoint price as probability)
    -- WHY: Best single estimate of fair value
    -- USAGE: Primary price for most analyses, ML target proxy
    -- CALCULATION: (YesBidProb + YesAskProb) / 2
    `MidProb` Float64,
    
    -- Primary market-implied probability for Yes
    -- WHY: Same as MidProb but explicit naming for ML
    -- USAGE: Probability estimation, model comparison
    `ImpliedProbYes` Float64,
    
    -- =========================================================================
    -- Volatility / Returns (L2 Features)
    -- Historical price movement features
    -- =========================================================================
    
    -- % price change over last 1 hour
    -- WHY: Short-term momentum signal
    -- USAGE: ML feature, trend detection, momentum strategies
    -- CALCULATION: (current_mid - mid_1h_ago) / mid_1h_ago
    `Return1h` Float64,
    
    -- % price change over last 24 hours
    -- WHY: Medium-term momentum signal
    -- USAGE: ML feature, trend strength, daily performance
    `Return24h` Float64,
    
    -- Realized volatility over 1h window (std dev of returns)
    -- WHY: Short-term uncertainty measure
    -- USAGE: Risk assessment, position sizing, options-like pricing
    -- CALCULATION: std(period returns) from candlesticks over 1h
    `Volatility1h` Float64,
    
    -- Realized volatility over 24h window
    -- WHY: Longer-term volatility for stable estimate
    -- USAGE: Volatility regime detection, risk normalization
    `Volatility24h` Float64,
    
    -- =========================================================================
    -- Liquidity & Depth (L1/L3 Features)
    -- Market microstructure features
    -- =========================================================================
    
    -- YesAskProb - YesBidProb (spread as probability)
    -- WHY: Key liquidity/cost indicator
    -- USAGE: Transaction cost estimation, liquidity scoring
    -- CALCULATION: YesAskProb - YesBidProb
    `BidAskSpread` Float64,
    
    -- Top of book liquidity for Yes side (size at best yes bid)
    -- WHY: Immediate executable quantity
    -- USAGE: Order size feasibility, slippage estimation
    -- SOURCE: First level of orderbook_snapshots.YesLevels
    `TopOfBookLiquidityYes` Float64,
    
    -- Top of book liquidity for No side
    `TopOfBookLiquidityNo` Float64,
    
    -- Total Yes side liquidity (sum of all levels)
    -- WHY: Full depth liquidity measure
    -- USAGE: Large order feasibility, overall market depth
    -- SOURCE: Sum of all sizes in orderbook_snapshots.YesLevels
    `TotalLiquidityYes` Float64,
    
    -- Total No side liquidity
    `TotalLiquidityNo` Float64,
    
    -- Orderbook imbalance: (Yes - No) / (Yes + No)
    -- WHY: Directional pressure indicator
    -- USAGE: Order flow prediction, sentiment proxy
    -- CALCULATION: (TotalLiquidityYes - TotalLiquidityNo) / (TotalLiquidityYes + TotalLiquidityNo)
    -- RANGE: -1 (all No) to +1 (all Yes)
    `OrderbookImbalance` Float64,
    
    -- =========================================================================
    -- Volume / Activity (L1/L2 Features)
    -- Trading activity measures
    -- =========================================================================
    
    -- Contracts traded in last 1 hour
    -- WHY: Recent activity measure
    -- USAGE: Activity filtering, momentum confirmation
    -- SOURCE: Sum of candlestick volumes over 1h
    `Volume1h` Float64,
    
    -- Contracts traded in last 24 hours
    -- WHY: Daily activity measure (from snapshot)
    -- USAGE: Liquidity proxy, market interest indicator
    -- SOURCE: market_snapshots.Volume24h
    `Volume24h` Float64,
    
    -- Open interest (outstanding contracts)
    -- WHY: Market commitment indicator
    -- USAGE: Market maturity, potential for price impact
    -- SOURCE: market_snapshots.OpenInterest
    `OpenInterest` Float64,
    
    -- Notional value traded in 1h (volume * price)
    -- WHY: Dollar-weighted activity measure
    -- USAGE: Size-weighted rankings, value at risk
    -- CALCULATION: Sum(volume * price) from candlesticks over 1h
    `Notional1h` Float64,
    
    -- Notional value traded in 24h
    `Notional24h` Float64,
    
    -- =========================================================================
    -- Categorical Features
    -- Non-numeric features for stratification
    -- =========================================================================
    
    -- Category from market_series or market_events
    -- WHY: Category-based model stratification
    -- USAGE: Category-specific models, filtering
    -- SOURCE: market_events.Category or market_series.Category
    `Category` String,
    
    -- Market type from market_snapshots (binary, ranged, etc.)
    -- WHY: Different market types behave differently
    -- USAGE: Market type-specific models
    -- SOURCE: market_snapshots.MarketType
    `MarketType` String,
    
    -- Market status: Active, Closed, Settled
    -- WHY: Only Active markets are tradeable
    -- USAGE: Filtering, status-based analysis
    -- SOURCE: market_snapshots.Status
    `Status` String,
    
    -- =========================================================================
    -- External / Factual Probability (Placeholder)
    -- For future integration with external probability sources
    -- =========================================================================
    
    -- Real-world estimate of true probability of "Yes"
    -- WHY: External reference probability for calibration
    -- USAGE: Model calibration, misprice detection
    -- NOTE: Currently placeholder (0.0), to be populated by external sources
    `FactualProbabilityYes` Float64,
    
    -- Misprice score: how wrong market price is vs factual probability
    -- WHY: Signal for potential trading opportunity
    -- USAGE: Alpha signal, model validation
    -- CALCULATION: |ImpliedProbYes - FactualProbabilityYes| or more sophisticated
    -- RANGE: 0 (perfect alignment) to 1 (maximum misprice)
    `MispriceScore` Float64,
    
    -- =========================================================================
    -- Record Metadata
    -- =========================================================================
    
    -- When this feature row was computed/written
    -- WHY: Distinguish FeatureTime (data time) from computation time
    -- USAGE: Debug latency, track processing delays
    `GeneratedAt` DateTime,
    
    INDEX idx_analytics_market_features_ticker Ticker TYPE bloom_filter GRANULARITY 1,
    INDEX idx_analytics_market_features_feature_time FeatureTime TYPE minmax GRANULARITY 1
)
ENGINE = MergeTree()
ORDER BY (Ticker, FeatureTime)
SETTINGS index_granularity = 8192;


-- =============================================================================
-- RELATIONSHIPS AND DATA FLOW
-- =============================================================================
-- 
-- HIERARCHY:
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
--               ┌───────────┼───────────┐
--               │           │           │
--    market_candlesticks  orderbook_   analytics_market_
--        (OHLC data)      snapshots      features
--                         (depth)       (ML features)
--                            │
--                            │
--                     orderbook_events
--                      (granular changes)
--
-- SUPPORTING TABLES:
--   - market_categories: Denormalized category view
--   - TagsCategories: Tag-to-category mapping
--   - Users: Application user data
--
-- =============================================================================
-- 
-- DATA SYNC ENDPOINTS AND CONSUMERS:
-- 
-- 1. /api/private/data-source/sync-market-snapshots
--    └── Fetches current market data → market_snapshots
--    └── Primary data source, runs frequently (every minute)
-- 
-- 2. /api/private/data-source/sync-candlesticks
--    └── For markets in market_highpriority (FetchCandlesticks=1)
--    └── Fetches OHLC data → market_candlesticks
--    └── Consumer: SynchronizeCandlesticksConsumer
-- 
-- 3. /api/private/data-source/sync-orderbook
--    └── For markets in market_highpriority (FetchOrderbook=1)
--    └── Fetches orderbook depth → orderbook_snapshots
--    └── Consumer: SynchronizeOrderbookConsumer
--    └── Also generates → orderbook_events (via diffing)
-- 
-- 4. /api/private/data-source/sync-series
--    └── Fetches series metadata → market_series
--    └── Runs periodically (daily or on-demand)
-- 
-- 5. /api/private/data-source/sync-events
--    └── Fetches event metadata → market_events
--    └── Runs periodically
-- 
-- 6. /api/private/data-source/sync-tags-categories
--    └── Fetches tag/category mapping → TagsCategories
-- 
-- 7. /api/analytics/process-analytics (internal)
--    └── For markets in market_highpriority (ProcessAnalyticsL1/L2/L3)
--    └── Computes features → analytics_market_features
--    └── Consumer: ProcessMarketAnalyticsConsumer
--    └── Service: AnalyticsService.ProcessMarketAnalyticsAsync()
-- 
-- =============================================================================
-- 
-- ANALYTICS PIPELINE FEATURE LEVELS:
-- 
-- L1 (Basic Features) - ProcessAnalyticsL1
--    Source: market_snapshots (current snapshot only)
--    Features:
--      - TimeToCloseSeconds, TimeToExpirationSeconds
--      - YesBidProb, YesAskProb, NoBidProb, NoAskProb
--      - MidProb, ImpliedProbYes
--      - BidAskSpread
--      - Volume24h, OpenInterest
--      - MarketType, Status
--    Speed: Fast (single snapshot, no history)
-- 
-- L2 (Historical Features) - ProcessAnalyticsL2
--    Source: market_snapshots (historical) + market_candlesticks
--    Features:
--      - Return1h, Return24h
--      - Volatility1h, Volatility24h
--      - Volume1h, Notional1h, Notional24h
--    Speed: Medium (requires historical queries)
-- 
-- L3 (Advanced Features) - ProcessAnalyticsL3
--    Source: orderbook_snapshots + market_events
--    Features:
--      - TopOfBookLiquidityYes, TopOfBookLiquidityNo
--      - TotalLiquidityYes, TotalLiquidityNo
--      - OrderbookImbalance
--      - Category (from events/series lookup)
--    Speed: Medium (requires orderbook + event queries)
-- 
-- =============================================================================
-- 
-- EXAMPLE QUERIES:
-- 
-- 1. Get latest snapshot for each active market:
--    SELECT * FROM market_snapshots 
--    WHERE Status = 'Active' 
--    AND (Ticker, GenerateDate) IN (
--        SELECT Ticker, MAX(GenerateDate) FROM market_snapshots GROUP BY Ticker
--    )
-- 
-- 2. Get candlesticks for charting (1-hour candles, last 7 days):
--    SELECT * FROM market_candlesticks
--    WHERE Ticker = 'KXBTC-24DEC31-B100000'
--    AND PeriodInterval = 60
--    AND EndPeriodTime >= now() - INTERVAL 7 DAY
--    ORDER BY EndPeriodTime
-- 
-- 3. Get latest analytics features for high-priority markets:
--    SELECT f.* FROM analytics_market_features f
--    INNER JOIN market_highpriority h ON f.Ticker = h.TickerId
--    WHERE (f.Ticker, f.FeatureTime) IN (
--        SELECT Ticker, MAX(FeatureTime) FROM analytics_market_features GROUP BY Ticker
--    )
-- 
-- 4. Find markets with high orderbook imbalance (potential momentum):
--    SELECT Ticker, OrderbookImbalance, MidProb, Volume24h
--    FROM analytics_market_features
--    WHERE FeatureTime = (SELECT MAX(FeatureTime) FROM analytics_market_features WHERE Ticker = analytics_market_features.Ticker)
--    AND ABS(OrderbookImbalance) > 0.3
--    ORDER BY ABS(OrderbookImbalance) DESC
-- 
-- 5. Track price evolution with features over time:
--    SELECT FeatureTime, MidProb, BidAskSpread, Volume24h, Volatility24h
--    FROM analytics_market_features
--    WHERE Ticker = 'KXBTC-24DEC31-B100000'
--    ORDER BY FeatureTime
-- 
-- =============================================================================

