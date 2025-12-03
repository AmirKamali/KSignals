CREATE OR REPLACE VIEW kalshi_signals.market_snapshots_latest AS
WITH latest AS (
    SELECT 
        Ticker,
        max(GenerateDate) AS MaxGenerateDate
    FROM kalshi_signals.market_snapshots
    GROUP BY Ticker
)
SELECT ms.*
FROM kalshi_signals.market_snapshots AS ms
INNER JOIN latest AS l
    ON ms.Ticker = l.Ticker
   AND ms.GenerateDate = l.MaxGenerateDate;