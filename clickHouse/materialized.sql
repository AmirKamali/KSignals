DROP TABLE IF EXISTS kalshi_signals.market_snapshots_latest;
CREATE TABLE IF NOT EXISTS kalshi_signals.market_snapshots_latest(
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
    
    -- Associated event ticker (e.g., 'BEYONCEGENRE', 'BTC-24DEC31')
    -- WHY: Links to specific event within a series
    -- USAGE: Event-level filtering, grouping markets by event
    -- NOTE: SeriesTicker can be looked up via market_events table using EventTicker
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
    `GenerateDate` DateTime,
)
ENGINE = ReplacingMergeTree(GenerateDate)
ORDER BY (EventTicker);  -- or (Ticker, EventTicker) if you want that pair unique



CREATE MATERIALIZED VIEW IF NOT EXISTS kalshi_signals.mv_market_snapshots_latest
TO kalshi_signals.market_snapshots_latest
AS
SELECT
-- Primary identifier (auto-generated UUID)
    -- WHY: Ensures globally unique identifier for each snapshot
    -- USAGE: Used as primary key, referenced in foreign key relationships
    `MarketSnapshotID`,
    
    -- =========================================================================
    -- Market Identifiers
    -- These fields link the snapshot to market, series, and event hierarchies
    -- =========================================================================
    
    -- Unique market ticker (e.g., 'BEYONCEGENRE-30-AFA', 'KXBTC-24DEC31-B100000')
    -- WHY: Primary identifier from Kalshi API, human-readable
    -- USAGE: Main filter/join key, displayed in UI, used in API calls
    `Ticker`,
    
    -- Associated event ticker (e.g., 'BEYONCEGENRE', 'BTC-24DEC31')
    -- WHY: Links to specific event within a series
    -- USAGE: Event-level filtering, grouping markets by event
    -- NOTE: SeriesTicker can be looked up via market_events table using EventTicker
    `EventTicker`,
    
    -- =========================================================================
    -- Market Metadata
    -- Descriptive information about the market structure
    -- =========================================================================
    
    -- Type of market (binary, ranged, etc.)
    -- WHY: Different market types have different pricing/settlement mechanics
    -- USAGE: UI display logic, analytics stratification
    `MarketType`,
    
    -- Display subtitle for Yes outcome (e.g., "Trump wins", "Over 100k")
    -- WHY: Human-readable description of what "Yes" means
    -- USAGE: Frontend display, search indexing
    `YesSubTitle`,
    
    -- Display subtitle for No outcome
    -- WHY: Human-readable description of what "No" means
    -- USAGE: Frontend display, search indexing
    `NoSubTitle`,
    
    -- =========================================================================
    -- Timestamps
    -- Critical time points in market lifecycle
    -- =========================================================================
    
    -- When market was created on Kalshi
    -- WHY: Track market age, sort by recency
    -- USAGE: "New markets" filtering, age-based analytics
    `CreatedTime`,
    
    -- When market opened for trading
    -- WHY: Trading may start after creation
    -- USAGE: Calculate active trading duration
    `OpenTime`,
    
    -- When market closes/closed for trading
    -- WHY: Critical for time-to-close calculations, trading cutoff
    -- USAGE: Countdown displays, TimeToCloseSeconds calculation
    `CloseTime`,
    
    -- Expected settlement time (may be null if not determined)
    -- WHY: When outcome is expected to be known
    -- USAGE: Settlement countdown, planning analytics
    `ExpectedExpirationTime` ,
    
    -- Latest possible expiration/settlement
    -- WHY: Absolute deadline for settlement
    -- USAGE: Risk calculations, worst-case planning
    `LatestExpirationTime`,
    
    -- Fee waiver end time (promotional periods)
    -- WHY: Kalshi may waive fees for new markets
    -- USAGE: Display fee status, trading cost calculations
    `FeeWaiverExpirationTime` ,
    
    -- =========================================================================
    -- Settlement Information
    -- Data related to market resolution
    -- =========================================================================
    
    -- Final settlement value (cents) - null until settled
    -- WHY: The resolved outcome value (0 or 100 for binary)
    -- USAGE: P&L calculations, historical outcome analysis
    `SettlementValue` ,
    
    -- Final settlement value (dollars) - formatted string
    -- WHY: Dollar-formatted for display
    -- USAGE: Direct UI display without conversion
    `SettlementValueDollars` ,
    
    -- =========================================================================
    -- Market Status
    -- Current state and outcome information
    -- =========================================================================
    
    -- Market status: 'Active', 'Closed', 'Settled', 'Inactive'
    -- WHY: Determines if market is tradeable and data freshness
    -- USAGE: Filter active markets, status badges in UI
    `Status`,
    
    -- Outcome result after settlement: 'yes', 'no', '' (empty if not settled)
    -- WHY: The final determined outcome
    -- USAGE: Historical analysis, P&L calculations
    `Result`,
    
    -- Boolean: can market close early (1=true, 0=false)
    -- WHY: Some markets can resolve before scheduled close
    -- USAGE: Risk warnings, trading strategy considerations
    `CanCloseEarly`,
    
    -- =========================================================================
    -- Current Pricing (in cents and dollars)
    -- Core trading prices at snapshot time
    -- =========================================================================
    
    -- Price unit type from API response
    -- WHY: Indicates how prices are denominated
    -- USAGE: Price parsing logic
    `ResponsePriceUnits`,
    
    -- Best Yes bid price (cents, 0-100)
    -- WHY: Highest price someone will pay for Yes
    -- USAGE: Trading decisions, spread calculations, analytics
    `YesBid` ,
    
    -- Best Yes bid price (dollars formatted, e.g., "0.45")
    -- WHY: Pre-formatted for display
    -- USAGE: Direct UI display
    `YesBidDollars`,
    
    -- Best Yes ask price (cents, 0-100)
    -- WHY: Lowest price someone will sell Yes for
    -- USAGE: Trading decisions, spread calculations
    `YesAsk` ,
    
    -- Best Yes ask price (dollars)
    `YesAskDollars`,
    
    -- Best No bid price (cents)
    -- WHY: Highest price for No contracts (inverse of Yes)
    -- USAGE: Arbitrage detection, No-side trading
    `NoBid` ,
    
    `NoBidDollars`,
    
    -- Best No ask price (cents)
    `NoAsk` ,
    
    `NoAskDollars`,
    
    -- Last traded price (cents)
    -- WHY: Most recent execution price
    -- USAGE: Current price display, change calculations
    `LastPrice` ,
    
    `LastPriceDollars`,
    
    -- =========================================================================
    -- Previous Prices (for change calculation)
    -- Prices from previous snapshot for delta calculations
    -- =========================================================================
    
    -- Previous Yes bid (cents)
    -- WHY: Calculate price movement, display change indicators
    -- USAGE: "Up 5%" badges, trend arrows
    `PreviousYesBid`,
    `PreviousYesBidDollars`,
    
    -- Previous Yes ask (cents)
    `PreviousYesAsk`,
    `PreviousYesAskDollars`,
    
    -- Previous last traded price (cents)
    `PreviousPrice`,
    `PreviousPriceDollars`,
    
    -- =========================================================================
    -- Volume and Liquidity
    -- Trading activity and market depth indicators
    -- =========================================================================
    
    -- Total contracts ever traded
    -- WHY: Lifetime volume indicates market maturity/interest
    -- USAGE: Sorting by popularity, liquidity assessment
    `Volume`,
    
    -- Contracts traded in last 24 hours
    -- WHY: Recent activity indicator
    -- USAGE: "Hot markets" filtering, activity badges
    `Volume24h`,
    
    -- Open contract positions (outstanding contracts)
    -- WHY: Indicates market depth and commitment
    -- USAGE: Liquidity scoring, market health indicators
    `OpenInterest`,
    
    -- Total notional value (cents) = volume * price
    -- WHY: Dollar-weighted activity measure
    -- USAGE: Size-weighted rankings, institutional interest proxy
    `NotionalValue`,
    `NotionalValueDollars`,
    
    -- Available liquidity (cents) - total value available to trade
    -- WHY: How much can be traded without moving price
    -- USAGE: Large order feasibility, slippage estimation
    `Liquidity`,
    `LiquidityDollars`,
    
    -- =========================================================================
    -- Strike/Price Structure
    -- Market-specific pricing parameters
    -- =========================================================================
    
    -- Value at expiration (description or value)
    -- WHY: What the market settles to
    -- USAGE: Rules display, settlement logic
    `ExpirationValue`,
    
    -- Minimum price increment (typically 1 cent)
    -- WHY: Defines valid price levels
    -- USAGE: Price validation, orderbook display
    `TickSize`,
    
    -- Type of strike (for ranged markets)
    -- WHY: Categorizes strike structure
    -- USAGE: Market type-specific logic
    `StrikeType` ,
    
    -- Floor strike price (for ranged markets)
    -- WHY: Lower bound of range
    -- USAGE: Ranged market calculations
    `FloorStrike` ,
    
    -- Cap strike price (for ranged markets)
    -- WHY: Upper bound of range
    -- USAGE: Ranged market calculations
    `CapStrike` ,
    
    -- Functional strike definition
    -- WHY: Mathematical definition of strike
    -- USAGE: Advanced analytics
    `FunctionalStrike` ,
    
    -- Custom strike (JSON string for complex strikes)
    -- WHY: Flexible strike definitions
    -- USAGE: Complex market types
    `CustomStrike` ,
    
    -- =========================================================================
    -- Rules and Metadata
    -- Market rules and auxiliary information
    -- =========================================================================
    
    -- MVE (Multi-Variable Event) collection ticker
    -- WHY: Links to parent MVE structure
    -- USAGE: MVE market grouping
    `MveCollectionTicker` ,
    
    -- MVE selected legs (JSON)
    -- WHY: Which legs of MVE are selected
    -- USAGE: MVE position tracking
    `MveSelectedLegs` ,
    
    -- Primary participant identifier
    -- WHY: Main entity in market (e.g., candidate name)
    -- USAGE: Search, filtering by participant
    `PrimaryParticipantKey` ,
    
    -- Price level structure type
    -- WHY: How price levels are organized
    -- USAGE: Orderbook rendering
    `PriceLevelStructure`,
    
    -- Price ranges (JSON for ranged markets)
    -- WHY: Defines valid price ranges
    -- USAGE: Ranged market display
    `PriceRanges` ,
    
    -- =========================================================================
    -- Record Metadata
    -- =========================================================================
    
    -- When this snapshot was captured
    -- WHY: Primary time dimension for time-series queries
    -- USAGE: ORDER BY, time-range filters, data freshness
    `GenerateDate`,
FROM kalshi_signals.market_snapshots;