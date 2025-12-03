-- Create the materialized view
-- Note: Run view_market_snapshot_latest_table.sql first to create the target table
-- 
-- Solution: Since argMax(GenerateDate, GenerateDate) conflicts when used with other argMax calls,
-- we compute GenerateDate by selecting the max value from a subquery result.
-- We use the fact that all argMax values come from the row with max GenerateDate.
CREATE MATERIALIZED VIEW IF NOT EXISTS kalshi_signals.mv_market_snapshots_latest
TO kalshi_signals.market_snapshots_latest
AS
SELECT
    argMax(MarketSnapshotID, GenerateDate) AS MarketSnapshotID,
    Ticker                                 AS Ticker,
    argMax(EventTicker, GenerateDate)      AS EventTicker,
    argMax(MarketType, GenerateDate)       AS MarketType,
    argMax(YesSubTitle, GenerateDate)      AS YesSubTitle,
    argMax(NoSubTitle, GenerateDate)       AS NoSubTitle,
    argMax(Status, GenerateDate)           AS Status,
    argMax(Result, GenerateDate)           AS Result,
    argMax(YesBid, GenerateDate)           AS YesBid,
    argMax(YesAsk, GenerateDate)           AS YesAsk,
    argMax(NoBid, GenerateDate)            AS NoBid,
    argMax(NoAsk, GenerateDate)            AS NoAsk,
    argMax(LastPrice, GenerateDate)        AS LastPrice,
    argMax(Volume, GenerateDate)           AS Volume,
    argMax(Volume24h, GenerateDate)        AS Volume24h,
    argMax(OpenInterest, GenerateDate)     AS OpenInterest,
    argMax(Liquidity, GenerateDate)        AS Liquidity,
    argMax(CreatedTime, GenerateDate)      AS CreatedTime,
    argMax(OpenTime, GenerateDate)         AS OpenTime,
    argMax(CloseTime, GenerateDate)        AS CloseTime,
    (SELECT max(GenerateDate) FROM kalshi_signals.market_snapshots ms2 WHERE ms2.Ticker = market_snapshots.Ticker) AS GenerateDate
FROM kalshi_signals.market_snapshots
GROUP BY Ticker
