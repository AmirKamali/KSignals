-- Migration: Add Component column to sync_logs table
-- Description: Adds a Component column to track which class/component generated the log
-- Database: ClickHouse

-- Add Component column with default value empty string
ALTER TABLE kalshi_signals.sync_logs
ADD COLUMN Component String DEFAULT '';

-- Verify the column was added
DESCRIBE kalshi_signals.sync_logs;

-- Optional: View sample data with the new column
SELECT * FROM kalshi_signals.sync_logs LIMIT 5;
