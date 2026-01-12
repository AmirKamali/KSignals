-- Migration: Add Volume column to market_series table
-- Description: Adds Volume column to track total contracts traded across all events in a series
-- Database: ClickHouse

-- Add Volume column (nullable Int64)
ALTER TABLE kalshi_signals.market_series
ADD COLUMN Volume Nullable(Int64);

-- Verify the column was added
DESCRIBE kalshi_signals.market_series;

-- Optional: View sample data with the new column
SELECT Ticker, Title, Category, Volume, LastUpdate
FROM kalshi_signals.market_series
LIMIT 10;
