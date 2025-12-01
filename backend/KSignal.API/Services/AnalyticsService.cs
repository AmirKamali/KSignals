using KSignal.API.Data;
using KSignal.API.Messaging;
using KSignal.API.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KSignal.API.Services;

/// <summary>
/// Service for processing and computing market analytics features.
/// Reads from market_highpriority and populates analytics_market_features.
/// </summary>
public class AnalyticsService
{
    private readonly KalshiDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        KalshiDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<AnalyticsService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Enqueues analytics processing jobs for all tickers in market_highpriority
    /// </summary>
    public async Task EnqueueMarketAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all tickers from market_highpriority that have at least one analytics flag enabled
            var highPriorityMarkets = await _dbContext.MarketHighPriorities
                .AsNoTracking()
                .Where(m => m.ProcessAnalyticsL1 || m.ProcessAnalyticsL2 || m.ProcessAnalyticsL3)
                .OrderByDescending(m => m.Priority)
                .ToListAsync(cancellationToken);

            if (highPriorityMarkets.Count == 0)
            {
                _logger.LogWarning("No high-priority markets configured for analytics processing");
                return;
            }

            _logger.LogInformation("Queueing analytics processing for {Count} high-priority markets", highPriorityMarkets.Count);

            foreach (var market in highPriorityMarkets)
            {
                await _publishEndpoint.Publish(
                    new ProcessMarketAnalytics(
                        market.TickerId,
                        market.ProcessAnalyticsL1,
                        market.ProcessAnalyticsL2,
                        market.ProcessAnalyticsL3),
                    cancellationToken);
            }

            _logger.LogInformation("Successfully queued {Count} analytics processing jobs", highPriorityMarkets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueueing market analytics jobs");
            throw;
        }
    }

    /// <summary>
    /// Processes analytics for a specific market ticker and inserts features into analytics_market_features
    /// </summary>
    public async Task ProcessMarketAnalyticsAsync(
        string tickerId,
        bool processL1,
        bool processL2,
        bool processL3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Processing analytics for ticker {TickerId}", tickerId);

            // Get the latest market snapshot for this ticker
            var snapshot = await _dbContext.MarketSnapshots
                .AsNoTracking()
                .Where(s => s.Ticker == tickerId)
                .OrderByDescending(s => s.GenerateDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (snapshot == null)
            {
                _logger.LogWarning("No market snapshot found for ticker {TickerId}", tickerId);
                return;
            }

            // Look up the seriesId from market_events table using EventTicker
            var seriesId = string.Empty;
            if (!string.IsNullOrWhiteSpace(snapshot.EventTicker))
            {
                var marketEvent = await _dbContext.MarketEvents
                    .AsNoTracking()
                    .Where(e => e.EventTicker == snapshot.EventTicker)
                    .FirstOrDefaultAsync(cancellationToken);
                seriesId = marketEvent?.SeriesTicker ?? string.Empty;
            }

            var now = DateTime.UtcNow;
            var feature = new AnalyticsMarketFeature
            {
                Ticker = tickerId,
                SeriesId = seriesId,
                EventTicker = snapshot.EventTicker,
                FeatureTime = snapshot.GenerateDate,
                GeneratedAt = now
            };

            // L1: Basic features (prices, spreads, time structure)
            if (processL1)
            {
                await ComputeL1FeaturesAsync(feature, snapshot, cancellationToken);
            }

            // L2: Volatility and returns (requires historical data)
            if (processL2)
            {
                await ComputeL2FeaturesAsync(feature, tickerId, snapshot.GenerateDate, cancellationToken);
            }

            // L3: Advanced metrics (orderbook imbalance, etc.)
            if (processL3)
            {
                await ComputeL3FeaturesAsync(feature, tickerId, cancellationToken);
            }

            // FeatureId is auto-generated by ClickHouse via generateUUIDv4()
            await _dbContext.AnalyticsMarketFeatures.AddAsync(feature, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully processed analytics for ticker {TickerId}", tickerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analytics for ticker {TickerId}", tickerId);
            throw;
        }
    }

    private Task ComputeL1FeaturesAsync(AnalyticsMarketFeature feature, MarketSnapshot snapshot, CancellationToken cancellationToken)
    {
        // Time structure
        feature.TimeToCloseSeconds = (long)(snapshot.CloseTime - snapshot.GenerateDate).TotalSeconds;
        feature.TimeToExpirationSeconds = snapshot.ExpectedExpirationTime.HasValue
            ? (long)(snapshot.ExpectedExpirationTime.Value - snapshot.GenerateDate).TotalSeconds
            : 0;

        // Prices in probability space (0-1) - cents to probability
        feature.YesBidProb = (double)snapshot.YesBid / 100.0;
        feature.YesAskProb = (double)snapshot.YesAsk / 100.0;
        feature.NoBidProb = (double)snapshot.NoBid / 100.0;
        feature.NoAskProb = (double)snapshot.NoAsk / 100.0;
        feature.MidProb = (feature.YesBidProb + feature.YesAskProb) / 2.0;
        feature.ImpliedProbYes = feature.MidProb;

        // Spread
        feature.BidAskSpread = feature.YesAskProb - feature.YesBidProb;

        // Basic volume and activity from snapshot
        feature.Volume24h = snapshot.Volume24h;
        feature.OpenInterest = snapshot.OpenInterest;

        // Categorical data
        feature.MarketType = snapshot.MarketType;
        feature.Status = snapshot.Status;

        return Task.CompletedTask;
    }

    private async Task ComputeL2FeaturesAsync(
        AnalyticsMarketFeature feature,
        string tickerId,
        DateTime featureTime,
        CancellationToken cancellationToken)
    {
        // Get historical snapshots for return and volatility calculation
        var oneHourAgo = featureTime.AddHours(-1);
        var oneDayAgo = featureTime.AddHours(-24);

        // Get snapshot from ~1 hour ago
        var snapshot1h = await _dbContext.MarketSnapshots
            .AsNoTracking()
            .Where(s => s.Ticker == tickerId && s.GenerateDate <= oneHourAgo)
            .OrderByDescending(s => s.GenerateDate)
            .FirstOrDefaultAsync(cancellationToken);

        // Get snapshot from ~24 hours ago
        var snapshot24h = await _dbContext.MarketSnapshots
            .AsNoTracking()
            .Where(s => s.Ticker == tickerId && s.GenerateDate <= oneDayAgo)
            .OrderByDescending(s => s.GenerateDate)
            .FirstOrDefaultAsync(cancellationToken);

        // Calculate returns
        if (snapshot1h != null)
        {
            var price1h = ((double)snapshot1h.YesBid + (double)snapshot1h.YesAsk) / 200.0; // midpoint as probability
            var currentPrice = feature.MidProb;
            feature.Return1h = price1h > 0 ? (currentPrice - price1h) / price1h : 0;
        }

        if (snapshot24h != null)
        {
            var price24h = ((double)snapshot24h.YesBid + (double)snapshot24h.YesAsk) / 200.0;
            var currentPrice = feature.MidProb;
            feature.Return24h = price24h > 0 ? (currentPrice - price24h) / price24h : 0;
        }

        // Calculate volatility from candlesticks if available
        var candlesticks1h = await _dbContext.MarketCandlesticks
            .AsNoTracking()
            .Where(c => c.Ticker == tickerId && c.EndPeriodTime >= oneHourAgo && c.EndPeriodTime <= featureTime)
            .OrderBy(c => c.EndPeriodTime)
            .ToListAsync(cancellationToken);

        var candlesticks24h = await _dbContext.MarketCandlesticks
            .AsNoTracking()
            .Where(c => c.Ticker == tickerId && c.EndPeriodTime >= oneDayAgo && c.EndPeriodTime <= featureTime)
            .OrderBy(c => c.EndPeriodTime)
            .ToListAsync(cancellationToken);

        feature.Volatility1h = CalculateVolatility(candlesticks1h);
        feature.Volatility24h = CalculateVolatility(candlesticks24h);

        // Volume features from candlesticks
        feature.Volume1h = candlesticks1h.Sum(c => c.Volume);
        feature.Notional1h = candlesticks1h.Sum(c => c.Volume * (c.PriceClose ?? c.YesBidClose) / 100.0);
        feature.Notional24h = candlesticks24h.Sum(c => c.Volume * (c.PriceClose ?? c.YesBidClose) / 100.0);
    }

    private async Task ComputeL3FeaturesAsync(
        AnalyticsMarketFeature feature,
        string tickerId,
        CancellationToken cancellationToken)
    {
        // Get latest orderbook snapshot
        var orderbookSnapshot = await _dbContext.OrderbookSnapshots
            .AsNoTracking()
            .Where(o => o.MarketId == tickerId)
            .OrderByDescending(o => o.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (orderbookSnapshot != null)
        {
            feature.TotalLiquidityYes = orderbookSnapshot.TotalYesLiquidity;
            feature.TotalLiquidityNo = orderbookSnapshot.TotalNoLiquidity;

            // Calculate orderbook imbalance
            var totalLiquidity = feature.TotalLiquidityYes + feature.TotalLiquidityNo;
            feature.OrderbookImbalance = totalLiquidity > 0
                ? (feature.TotalLiquidityYes - feature.TotalLiquidityNo) / totalLiquidity
                : 0;

            // Top of book liquidity from parsed levels
            if (!string.IsNullOrEmpty(orderbookSnapshot.YesLevels))
            {
                try
                {
                    var yesLevels = JsonConvert.DeserializeObject<List<List<decimal>>>(orderbookSnapshot.YesLevels);
                    feature.TopOfBookLiquidityYes = yesLevels?.Count > 0 ? (double)yesLevels[0][1] : 0;
                }
                catch { /* Ignore parsing errors */ }
            }

            if (!string.IsNullOrEmpty(orderbookSnapshot.NoLevels))
            {
                try
                {
                    var noLevels = JsonConvert.DeserializeObject<List<List<decimal>>>(orderbookSnapshot.NoLevels);
                    feature.TopOfBookLiquidityNo = noLevels?.Count > 0 ? (double)noLevels[0][1] : 0;
                }
                catch { /* Ignore parsing errors */ }
            }
        }

        // Get category from market series or events
        var marketEvent = await _dbContext.MarketEvents
            .AsNoTracking()
            .Where(e => e.EventTicker == feature.EventTicker)
            .FirstOrDefaultAsync(cancellationToken);

        if (marketEvent != null)
        {
            feature.Category = marketEvent.Category;
        }
        else
        {
            // Try from series
            var series = await _dbContext.MarketSeries
                .AsNoTracking()
                .Where(s => s.Ticker == feature.SeriesId)
                .FirstOrDefaultAsync(cancellationToken);

            feature.Category = series?.Category ?? string.Empty;
        }
    }

    private static double CalculateVolatility(List<MarketCandlestickData> candlesticks)
    {
        if (candlesticks.Count < 2)
            return 0;

        // Calculate returns between consecutive candlesticks
        var returns = new List<double>();
        for (int i = 1; i < candlesticks.Count; i++)
        {
            var prevPrice = (candlesticks[i - 1].PriceClose ?? candlesticks[i - 1].YesBidClose) / 100.0;
            var currPrice = (candlesticks[i].PriceClose ?? candlesticks[i].YesBidClose) / 100.0;
            
            if (prevPrice > 0)
            {
                returns.Add((currPrice - prevPrice) / prevPrice);
            }
        }

        if (returns.Count < 2)
            return 0;

        // Calculate standard deviation of returns
        var mean = returns.Average();
        var sumSquaredDiff = returns.Sum(r => Math.Pow(r - mean, 2));
        var variance = sumSquaredDiff / (returns.Count - 1);
        
        return Math.Sqrt(variance);
    }
}
