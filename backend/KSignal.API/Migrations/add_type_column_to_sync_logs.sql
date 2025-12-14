-- Migration: Add Type column to sync_logs table
-- Description: Adds a Type column to track log severity levels (Info, WARN, ERROR, DEBUG)
-- Database: ClickHouse

-- Add Type column with default value 'Info'
ALTER TABLE kalshi_signals.sync_logs
ADD COLUMN Type String DEFAULT 'Info';

-- Verify the column was added
DESCRIBE kalshi_signals.sync_logs;

-- Optional: View sample data with the new column
SELECT * FROM kalshi_signals.sync_logs LIMIT 5;
