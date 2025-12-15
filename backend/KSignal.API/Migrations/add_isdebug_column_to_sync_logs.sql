-- Migration: Add IsDebug column to sync_logs table
-- Description: Adds an IsDebug column to track whether the log was generated in Debug or Release mode
-- Database: ClickHouse

-- Add IsDebug column with default value false (0 for ClickHouse boolean)
ALTER TABLE kalshi_signals.sync_logs
ADD COLUMN IsDebug UInt8 DEFAULT 0;

-- Verify the column was added
DESCRIBE kalshi_signals.sync_logs;

-- Optional: View sample data with the new column
SELECT * FROM kalshi_signals.sync_logs LIMIT 5;
