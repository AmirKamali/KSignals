-- Migration script to update Markets table based on Kalshi API documentation
-- This script adds missing columns and drops deprecated columns
-- Reference: https://docs.kalshi.com/api-reference/market/get-market#response-market

-- ============================================
-- STEP 1: Add missing required columns
-- ============================================

-- EventTicker (event_ticker) - maps to SeriesTicker, but keeping both for now
-- Note: SeriesTicker already exists, EventTicker is the API field name
ALTER TABLE Markets ADD COLUMN EventTicker VARCHAR(255) NULL AFTER SeriesTicker;

-- MarketType (market_type) - enum: binary, scalar
ALTER TABLE Markets ADD COLUMN MarketType VARCHAR(20) NULL AFTER EventTicker;

-- YesSubTitle (yes_sub_title) - shortened title for yes side
ALTER TABLE Markets ADD COLUMN YesSubTitle TEXT NULL AFTER Subtitle;

-- NoSubTitle (no_sub_title) - shortened title for no side
ALTER TABLE Markets ADD COLUMN NoSubTitle TEXT NULL AFTER YesSubTitle;

-- ExpectedExpirationTime (expected_expiration_time) - nullable DateTime
ALTER TABLE Markets ADD COLUMN ExpectedExpirationTime DATETIME(6) NULL AFTER LatestExpirationTime;

-- SettlementTimerSeconds (settlement_timer_seconds) - int, required
ALTER TABLE Markets ADD COLUMN SettlementTimerSeconds INT NOT NULL DEFAULT 0 AFTER LatestExpirationTime;

-- ResponsePriceUnits (response_price_units) - enum: usd_cent
ALTER TABLE Markets ADD COLUMN ResponsePriceUnits VARCHAR(20) NULL DEFAULT 'usd_cent' AFTER Status;

-- Result (result) - enum: yes, no, ''
ALTER TABLE Markets ADD COLUMN Result VARCHAR(10) NULL AFTER Volume24h;

-- CanCloseEarly (can_close_early) - bool, required
ALTER TABLE Markets ADD COLUMN CanCloseEarly TINYINT(1) NOT NULL DEFAULT 0 AFTER Result;

-- OpenInterest (open_interest) - int, required
ALTER TABLE Markets ADD COLUMN OpenInterest INT NOT NULL DEFAULT 0 AFTER CanCloseEarly;

-- ExpirationValue (expiration_value) - string, required
ALTER TABLE Markets ADD COLUMN ExpirationValue TEXT NULL AFTER SettlementValueDollars;

-- FeeWaiverExpirationTime (fee_waiver_expiration_time) - nullable DateTime
ALTER TABLE Markets ADD COLUMN FeeWaiverExpirationTime DATETIME(6) NULL AFTER ExpirationValue;

-- EarlyCloseCondition (early_close_condition) - nullable string
ALTER TABLE Markets ADD COLUMN EarlyCloseCondition TEXT NULL AFTER FeeWaiverExpirationTime;

-- TickSize (tick_size) - int, required
ALTER TABLE Markets ADD COLUMN TickSize INT NOT NULL DEFAULT 1 AFTER EarlyCloseCondition;

-- StrikeType (strike_type) - nullable enum string
ALTER TABLE Markets ADD COLUMN StrikeType VARCHAR(30) NULL AFTER TickSize;

-- FloorStrike (floor_strike) - nullable double
ALTER TABLE Markets ADD COLUMN FloorStrike DOUBLE NULL AFTER StrikeType;

-- CapStrike (cap_strike) - nullable double
ALTER TABLE Markets ADD COLUMN CapStrike DOUBLE NULL AFTER FloorStrike;

-- FunctionalStrike (functional_strike) - nullable string (JSON-like)
ALTER TABLE Markets ADD COLUMN FunctionalStrike TEXT NULL AFTER CapStrike;

-- CustomStrike (custom_strike) - nullable JSON object
ALTER TABLE Markets ADD COLUMN CustomStrike JSON NULL AFTER FunctionalStrike;

-- RulesPrimary (rules_primary) - string, required
ALTER TABLE Markets ADD COLUMN RulesPrimary TEXT NULL AFTER CustomStrike;

-- RulesSecondary (rules_secondary) - string, required
ALTER TABLE Markets ADD COLUMN RulesSecondary TEXT NULL AFTER RulesPrimary;

-- MveCollectionTicker (mve_collection_ticker) - nullable string
ALTER TABLE Markets ADD COLUMN MveCollectionTicker VARCHAR(255) NULL AFTER RulesSecondary;

-- MveSelectedLegs (mve_selected_legs) - nullable JSON array
ALTER TABLE Markets ADD COLUMN MveSelectedLegs JSON NULL AFTER MveCollectionTicker;

-- PrimaryParticipantKey (primary_participant_key) - nullable string
ALTER TABLE Markets ADD COLUMN PrimaryParticipantKey VARCHAR(255) NULL AFTER MveSelectedLegs;

-- PriceLevelStructure (price_level_structure) - string, required
ALTER TABLE Markets ADD COLUMN PriceLevelStructure TEXT NULL AFTER PrimaryParticipantKey;

-- PriceRanges (price_ranges) - JSON array, required
ALTER TABLE Markets ADD COLUMN PriceRanges JSON NULL AFTER PriceLevelStructure;

-- ============================================
-- STEP 2: Update existing columns if needed
-- ============================================

-- Update EventTicker from SeriesTicker if they should match
-- Uncomment if you want to copy data:
-- UPDATE Markets SET EventTicker = SeriesTicker WHERE EventTicker IS NULL;

-- ============================================
-- STEP 3: Drop deprecated columns (marked as deprecated in API)
-- ============================================

-- Note: Title and Subtitle are deprecated but may still be used in the app
-- Uncomment these lines if you're sure they're not needed:
-- ALTER TABLE Markets DROP COLUMN Title;
-- ALTER TABLE Markets DROP COLUMN Subtitle;

-- ExpirationTime is deprecated (use ExpectedExpirationTime and LatestExpirationTime instead)
-- Uncomment if you want to drop it:
-- ALTER TABLE Markets DROP COLUMN ExpirationTime;

-- Category is deprecated in API (but may be used elsewhere)
-- RiskLimitCents is deprecated in API
-- These are not in the current schema, so no action needed

-- ============================================
-- STEP 4: Add indexes for commonly queried fields
-- ============================================

-- Index on MarketType for filtering
CREATE INDEX IX_Markets_MarketType ON Markets(MarketType);

-- Index on Result for filtering
CREATE INDEX IX_Markets_Result ON Markets(Result);

-- Index on EventTicker if it's different from SeriesTicker
CREATE INDEX IX_Markets_EventTicker ON Markets(EventTicker);

-- ============================================
-- STEP 5: Update constraints and defaults
-- ============================================

-- Make SettlementTimerSeconds NOT NULL after data migration
-- ALTER TABLE Markets MODIFY COLUMN SettlementTimerSeconds INT NOT NULL;

-- Make ResponsePriceUnits NOT NULL after data migration
-- ALTER TABLE Markets MODIFY COLUMN ResponsePriceUnits VARCHAR(20) NOT NULL DEFAULT 'usd_cent';

-- Make CanCloseEarly NOT NULL after data migration
-- ALTER TABLE Markets MODIFY COLUMN CanCloseEarly TINYINT(1) NOT NULL DEFAULT 0;

-- Make OpenInterest NOT NULL after data migration
-- ALTER TABLE Markets MODIFY COLUMN OpenInterest INT NOT NULL DEFAULT 0;

-- Make TickSize NOT NULL after data migration
-- ALTER TABLE Markets MODIFY COLUMN TickSize INT NOT NULL DEFAULT 1;

-- ============================================
-- NOTES:
-- ============================================
-- 1. Review and uncomment the UPDATE statements if you need to migrate existing data
-- 2. Review and uncomment the DROP COLUMN statements for deprecated fields
-- 3. Some fields are marked as required in the API but added as nullable initially
--    to allow gradual migration. Update constraints after data is populated.
-- 4. JSON columns (CustomStrike, MveSelectedLegs, PriceRanges) require MySQL 5.7.8+
-- 5. Test this script on a development database first before running in production
-- 6. Consider creating a backup before running this migration


