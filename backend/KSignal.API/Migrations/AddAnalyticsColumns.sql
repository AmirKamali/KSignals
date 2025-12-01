-- Migration: Add analytics processing columns to market_highpriority
-- Run this once against existing databases to add the new columns
-- Default value of 1 (true) means existing rows will have analytics processing enabled

ALTER TABLE kalshi_signals.market_highpriority 
    ADD COLUMN IF NOT EXISTS `ProcessAnalyticsL1` UInt8 DEFAULT 1;

ALTER TABLE kalshi_signals.market_highpriority 
    ADD COLUMN IF NOT EXISTS `ProcessAnalyticsL2` UInt8 DEFAULT 1;

ALTER TABLE kalshi_signals.market_highpriority 
    ADD COLUMN IF NOT EXISTS `ProcessAnalyticsL3` UInt8 DEFAULT 1;
