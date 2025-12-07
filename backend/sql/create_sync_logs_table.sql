-- Create sync_logs table for tracking synchronization job enqueues
-- This table logs all sync operations performed by the SynchronizationService

CREATE TABLE IF NOT EXISTS sync_logs
(
    Id UUID DEFAULT generateUUIDv4(),
    EventName String,
    NumbersEnqueued Int32,
    LogDate DateTime
)
ENGINE = MergeTree()
ORDER BY (LogDate, EventName, Id)
PARTITION BY toYYYYMM(LogDate)
SETTINGS index_granularity = 8192;

-- Create index on EventName for efficient filtering by event type
-- ClickHouse uses data skipping indexes
CREATE INDEX IF NOT EXISTS idx_sync_logs_event_name ON sync_logs (EventName) TYPE bloom_filter(0.01) GRANULARITY 1;

-- Create index on LogDate for efficient time-range queries
-- Note: LogDate is already in ORDER BY, so it's automatically indexed
-- This minmax index provides additional optimization for date range queries
CREATE INDEX IF NOT EXISTS idx_sync_logs_log_date ON sync_logs (LogDate) TYPE minmax GRANULARITY 1;

-- Example queries to verify the table:
-- SELECT * FROM sync_logs ORDER BY LogDate DESC LIMIT 10;
-- SELECT EventName, COUNT(*) as count, SUM(NumbersEnqueued) as total_enqueued FROM sync_logs GROUP BY EventName;
-- SELECT toDate(LogDate) as date, EventName, COUNT(*) as count FROM sync_logs WHERE LogDate >= now() - INTERVAL 7 DAY GROUP BY date, EventName ORDER BY date DESC;
