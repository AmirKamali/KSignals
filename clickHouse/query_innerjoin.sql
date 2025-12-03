-- Query to join market_snapshots + market_events + market_series
-- 
-- Relationships:
--   market_snapshots.EventTicker = market_events.EventTicker
--   market_events.SeriesTicker = market_series.Ticker
--
-- Note: market_series and market_events use ReplacingMergeTree, so we use FINAL
--       in subqueries to get the latest version, and filter by IsDeleted = 0

SELECT 
    -- Market Snapshot fields
    ms.MarketSnapshotID as SnapShotID,
    ms.Ticker AS MarketTicker,
    ms.EventTicker,
    ms.MarketType,
    ms.YesSubTitle,
    ms.NoSubTitle,
    ms.Status,
    ms.Result,
    ms.YesBid,
    ms.YesAsk,
    ms.NoBid,
    ms.NoAsk,
    ms.LastPrice,
    ms.Volume,
    ms.Volume24h,
    ms.OpenInterest,
    ms.Liquidity,
    ms.CreatedTime,
    ms.OpenTime,
    ms.CloseTime,
    ms.GenerateDate,
    
    -- Event fields
    me.SeriesTicker as EventTickerID,
    me.Title AS EventTitle,
    me.SubTitle AS EventSubTitle,
    me.Category AS EventCategory,
    me.CollateralReturnType,
    me.MutuallyExclusive,
    me.StrikeDate,
    me.StrikePeriod,
    
    -- Series fields
    mser.Ticker AS SeriesTickerID,
    mser.Title AS SeriesTitle,
    mser.Category AS SeriesCategory,
    mser.Frequency,
    mser.Tags AS SeriesTags,
    mser.FeeType,
    mser.FeeMultiplier,

FROM kalshi_signals.market_snapshots AS ms
INNER JOIN (
    SELECT * FROM kalshi_signals.market_events FINAL
    WHERE IsDeleted = 0
) AS me 
    ON ms.EventTicker = me.EventTicker
INNER JOIN (
    SELECT * FROM kalshi_signals.market_series FINAL
    WHERE IsDeleted = 0
) AS mser 
    ON me.SeriesTicker = mser.Ticker

-- Optional: Add filters as needed
-- WHERE ms.Status = 'open'
--   AND mser.Category = 'Crypto'
--   AND ms.GenerateDate >= now() - INTERVAL 1 DAY
ORDER BY StrikePeriod desc
limit 20

