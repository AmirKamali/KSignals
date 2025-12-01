-- Migration: Add analytics processing columns to market_highpriority
-- Run this once against existing databases to add the new columns
-- Existing rows get value 1 (enabled), new rows default to 0 (disabled)

-- Step 1: Add columns with DEFAULT 1 (sets existing rows to 1)
ALTER TABLE kalshi_signals.market_highpriority 
    ADD COLUMN IF NOT EXISTS `ProcessAnalyticsL1` UInt8 DEFAULT 1;

ALTER TABLE kalshi_signals.market_highpriority 
    ADD COLUMN IF NOT EXISTS `ProcessAnalyticsL2` UInt8 DEFAULT 1;

ALTER TABLE kalshi_signals.market_highpriority 
    ADD COLUMN IF NOT EXISTS `ProcessAnalyticsL3` UInt8 DEFAULT 1;

-- Step 2: Change the default to 0 for future inserts
ALTER TABLE kalshi_signals.market_highpriority 
    MODIFY COLUMN `ProcessAnalyticsL1` UInt8 DEFAULT 0;

ALTER TABLE kalshi_signals.market_highpriority 
    MODIFY COLUMN `ProcessAnalyticsL2` UInt8 DEFAULT 0;

ALTER TABLE kalshi_signals.market_highpriority 
    MODIFY COLUMN `ProcessAnalyticsL3` UInt8 DEFAULT 0;
