-- Safe migration script to update Markets table based on Kalshi API documentation
-- This script checks for column existence before adding/dropping
-- Reference: https://docs.kalshi.com/api-reference/market/get-market#response-market

-- ============================================
-- STEP 1: Add missing required columns (with existence checks)
-- ============================================

-- EventTicker (event_ticker)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'EventTicker');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN EventTicker VARCHAR(255) NULL AFTER SeriesTicker', 
    'SELECT "EventTicker column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- MarketType (market_type) - enum: binary, scalar
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'MarketType');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN MarketType VARCHAR(20) NULL AFTER EventTicker', 
    'SELECT "MarketType column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- YesSubTitle (yes_sub_title)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'YesSubTitle');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN YesSubTitle TEXT NULL AFTER Subtitle', 
    'SELECT "YesSubTitle column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- NoSubTitle (no_sub_title)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'NoSubTitle');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN NoSubTitle TEXT NULL AFTER YesSubTitle', 
    'SELECT "NoSubTitle column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ExpectedExpirationTime (expected_expiration_time)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'ExpectedExpirationTime');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN ExpectedExpirationTime DATETIME(6) NULL AFTER LatestExpirationTime', 
    'SELECT "ExpectedExpirationTime column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- SettlementTimerSeconds (settlement_timer_seconds)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'SettlementTimerSeconds');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN SettlementTimerSeconds INT NOT NULL DEFAULT 0 AFTER LatestExpirationTime', 
    'SELECT "SettlementTimerSeconds column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ResponsePriceUnits (response_price_units)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'ResponsePriceUnits');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN ResponsePriceUnits VARCHAR(20) NULL DEFAULT ''usd_cent'' AFTER Status', 
    'SELECT "ResponsePriceUnits column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Result (result)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'Result');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN Result VARCHAR(10) NULL AFTER Volume24h', 
    'SELECT "Result column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- CanCloseEarly (can_close_early)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'CanCloseEarly');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN CanCloseEarly TINYINT(1) NOT NULL DEFAULT 0 AFTER Result', 
    'SELECT "CanCloseEarly column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- OpenInterest (open_interest)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'OpenInterest');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN OpenInterest INT NOT NULL DEFAULT 0 AFTER CanCloseEarly', 
    'SELECT "OpenInterest column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ExpirationValue (expiration_value)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'ExpirationValue');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN ExpirationValue TEXT NULL AFTER SettlementValueDollars', 
    'SELECT "ExpirationValue column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- FeeWaiverExpirationTime (fee_waiver_expiration_time)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'FeeWaiverExpirationTime');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN FeeWaiverExpirationTime DATETIME(6) NULL AFTER ExpirationValue', 
    'SELECT "FeeWaiverExpirationTime column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- EarlyCloseCondition (early_close_condition)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'EarlyCloseCondition');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN EarlyCloseCondition TEXT NULL AFTER FeeWaiverExpirationTime', 
    'SELECT "EarlyCloseCondition column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- TickSize (tick_size)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'TickSize');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN TickSize INT NOT NULL DEFAULT 1 AFTER EarlyCloseCondition', 
    'SELECT "TickSize column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- StrikeType (strike_type)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'StrikeType');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN StrikeType VARCHAR(30) NULL AFTER TickSize', 
    'SELECT "StrikeType column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- FloorStrike (floor_strike)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'FloorStrike');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN FloorStrike DOUBLE NULL AFTER StrikeType', 
    'SELECT "FloorStrike column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- CapStrike (cap_strike)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'CapStrike');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN CapStrike DOUBLE NULL AFTER FloorStrike', 
    'SELECT "CapStrike column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- FunctionalStrike (functional_strike)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'FunctionalStrike');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN FunctionalStrike TEXT NULL AFTER CapStrike', 
    'SELECT "FunctionalStrike column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- CustomStrike (custom_strike) - JSON
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'CustomStrike');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN CustomStrike JSON NULL AFTER FunctionalStrike', 
    'SELECT "CustomStrike column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- RulesPrimary (rules_primary)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'RulesPrimary');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN RulesPrimary TEXT NULL AFTER CustomStrike', 
    'SELECT "RulesPrimary column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- RulesSecondary (rules_secondary)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'RulesSecondary');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN RulesSecondary TEXT NULL AFTER RulesPrimary', 
    'SELECT "RulesSecondary column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- MveCollectionTicker (mve_collection_ticker)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'MveCollectionTicker');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN MveCollectionTicker VARCHAR(255) NULL AFTER RulesSecondary', 
    'SELECT "MveCollectionTicker column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- MveSelectedLegs (mve_selected_legs) - JSON
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'MveSelectedLegs');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN MveSelectedLegs JSON NULL AFTER MveCollectionTicker', 
    'SELECT "MveSelectedLegs column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- PrimaryParticipantKey (primary_participant_key)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'PrimaryParticipantKey');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN PrimaryParticipantKey VARCHAR(255) NULL AFTER MveSelectedLegs', 
    'SELECT "PrimaryParticipantKey column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- PriceLevelStructure (price_level_structure)
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'PriceLevelStructure');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN PriceLevelStructure TEXT NULL AFTER PrimaryParticipantKey', 
    'SELECT "PriceLevelStructure column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- PriceRanges (price_ranges) - JSON
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'PriceRanges');
SET @sql = IF(@col_exists = 0, 
    'ALTER TABLE Markets ADD COLUMN PriceRanges JSON NULL AFTER PriceLevelStructure', 
    'SELECT "PriceRanges column already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ============================================
-- STEP 2: Add indexes (with existence checks)
-- ============================================

-- Index on MarketType
SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND INDEX_NAME = 'IX_Markets_MarketType');
SET @sql = IF(@idx_exists = 0, 
    'CREATE INDEX IX_Markets_MarketType ON Markets(MarketType)', 
    'SELECT "IX_Markets_MarketType index already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Index on Result
SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND INDEX_NAME = 'IX_Markets_Result');
SET @sql = IF(@idx_exists = 0, 
    'CREATE INDEX IX_Markets_Result ON Markets(Result)', 
    'SELECT "IX_Markets_Result index already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Index on EventTicker
SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND INDEX_NAME = 'IX_Markets_EventTicker');
SET @sql = IF(@idx_exists = 0, 
    'CREATE INDEX IX_Markets_EventTicker ON Markets(EventTicker)', 
    'SELECT "IX_Markets_EventTicker index already exists" AS message');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ============================================
-- STEP 3: Drop deprecated columns (with existence checks)
-- ============================================
-- WARNING: Uncomment these only after verifying they're not used in your application

-- Drop ExpirationTime (deprecated, use ExpectedExpirationTime and LatestExpirationTime)
-- SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
--     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'ExpirationTime');
-- SET @sql = IF(@col_exists > 0, 
--     'ALTER TABLE Markets DROP COLUMN ExpirationTime', 
--     'SELECT "ExpirationTime column does not exist" AS message');
-- PREPARE stmt FROM @sql;
-- EXECUTE stmt;
-- DEALLOCATE PREPARE stmt;

-- Drop Title (deprecated in API, but may still be used in app)
-- SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
--     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'Title');
-- SET @sql = IF(@col_exists > 0, 
--     'ALTER TABLE Markets DROP COLUMN Title', 
--     'SELECT "Title column does not exist" AS message');
-- PREPARE stmt FROM @sql;
-- EXECUTE stmt;
-- DEALLOCATE PREPARE stmt;

-- Drop Subtitle (deprecated in API, but may still be used in app)
-- SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
--     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Markets' AND COLUMN_NAME = 'Subtitle');
-- SET @sql = IF(@col_exists > 0, 
--     'ALTER TABLE Markets DROP COLUMN Subtitle', 
--     'SELECT "Subtitle column does not exist" AS message');
-- PREPARE stmt FROM @sql;
-- EXECUTE stmt;
-- DEALLOCATE PREPARE stmt;

-- ============================================
-- STEP 4: Data migration (optional)
-- ============================================

-- Copy SeriesTicker to EventTicker if EventTicker is NULL
-- UPDATE Markets SET EventTicker = SeriesTicker WHERE EventTicker IS NULL;

-- ============================================
-- NOTES:
-- ============================================
-- 1. This script is idempotent - it can be run multiple times safely
-- 2. JSON columns require MySQL 5.7.8+ or MariaDB 10.2.7+
-- 3. Test on a development database first
-- 4. Create a backup before running in production
-- 5. Review deprecated column drops before uncommenting
-- 6. Some fields are added as nullable initially to allow gradual migration
