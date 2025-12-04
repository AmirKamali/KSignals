using Kalshi.Api;
using Kalshi.Api.Client;
using KSignal.API.Data;
using KSignal.API.Models;
using KSignals.DTO;
using Microsoft.EntityFrameworkCore;

namespace KSignal.API.Services;

public class ChartService
{
    private readonly KalshiClient _kalshiClient;
    private readonly KalshiDbContext _db;
    private readonly ILogger<ChartService> _logger;

    public ChartService(
        KalshiClient kalshiClient,
        KalshiDbContext db,
        ILogger<ChartService> logger)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets chart data for a market ticker, fetching from Kalshi API and storing in database.
    /// Uses efficient differential updates - only fetches new data since last stored candlestick.
    /// </summary>
    public async Task<ChartDataResponse> GetChartDataAsync(string ticker, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            throw new ArgumentException("Ticker is required", nameof(ticker));
        }

        try
        {
            _logger.LogInformation("Fetching chart data for ticker: {Ticker}", ticker);

            // First, get the market details to get the event ticker
            var marketResponse = await _kalshiClient.Markets.GetMarketAsync(ticker, cancellationToken: cancellationToken);
            if (marketResponse?.Market == null)
            {
                throw new InvalidOperationException($"Market {ticker} not found");
            }

            var eventTicker = marketResponse.Market.EventTicker;

            // Look up the series ticker from the event
            var marketEvent = await _db.MarketEvents.AsNoTracking()
                .FirstOrDefaultAsync(e => e.EventTicker == eventTicker, cancellationToken);

            if (marketEvent == null)
            {
                throw new InvalidOperationException($"Event {eventTicker} not found in database");
            }

            var seriesTicker = marketEvent.SeriesTicker;
            _logger.LogInformation("Series ticker for {Ticker}: {SeriesTicker}", ticker, seriesTicker);

            // Check for existing candlestick data in database
            var periodInterval = 1440; // 1 day
            var existingData = await _db.MarketCandlesticks
                .AsNoTracking()
                .Where(c => c.Ticker == ticker && c.PeriodInterval == periodInterval)
                .OrderBy(c => c.EndPeriodTs)
                .ToListAsync(cancellationToken);

            // Calculate time range
            var endTime = DateTime.UtcNow;
            var endTs = ((DateTimeOffset)endTime).ToUnixTimeSeconds();
            long startTs;

            if (existingData.Any())
            {
                // Get the latest candlestick timestamp
                var latestTs = existingData.Max(c => c.EndPeriodTs);
                var latestTime = DateTimeOffset.FromUnixTimeSeconds(latestTs).UtcDateTime;

                _logger.LogInformation("Found {Count} existing candlesticks for {Ticker}, latest: {LatestTime}",
                    existingData.Count, ticker, latestTime);

                // Only fetch data after the latest existing candlestick
                // Add 1 second to avoid duplicate
                startTs = latestTs + 1;

                // If the latest data is recent (within last day), we might not need to fetch anything new
                if (latestTime >= endTime.AddDays(-1))
                {
                    _logger.LogInformation("Existing data is up-to-date for {Ticker}, returning cached data", ticker);
                    return CreateResponseFromExistingData(ticker, existingData);
                }
            }
            else
            {
                // No existing data, fetch last 30 days
                var startTime = endTime.AddDays(-30);
                startTs = ((DateTimeOffset)startTime).ToUnixTimeSeconds();
                _logger.LogInformation("No existing data for {Ticker}, fetching 30 days", ticker);
            }

            // Fetch new candlestick data from Kalshi API
            _logger.LogInformation("Fetching differential candlesticks for {Ticker} from {StartTs} to {EndTs}",
                ticker, startTs, endTs);

            var candlesticksResponse = await _kalshiClient.Markets.GetMarketCandlesticksAsync(
                seriesTicker: seriesTicker,
                ticker: ticker,
                startTs: startTs,
                endTs: endTs,
                periodInterval: periodInterval,
                cancellationToken: cancellationToken
            );

            if (candlesticksResponse?.Candlesticks == null || !candlesticksResponse.Candlesticks.Any())
            {
                _logger.LogInformation("No new candlestick data returned for {Ticker}, returning existing data", ticker);
                return CreateResponseFromExistingData(ticker, existingData);
            }

            _logger.LogInformation("Received {Count} new candlesticks for {Ticker}",
                candlesticksResponse.Candlesticks.Count, ticker);

            // Save new candlesticks to database
            var fetchedAt = DateTime.UtcNow;
            var newCandlestickRecords = new List<MarketCandlestickData>();

            // Get existing timestamps to avoid duplicates
            var existingTimestamps = new HashSet<long>(existingData.Select(c => c.EndPeriodTs));

            foreach (var candle in candlesticksResponse.Candlesticks)
            {
                // Skip if we already have this candlestick
                if (existingTimestamps.Contains(candle.EndPeriodTs))
                {
                    _logger.LogDebug("Skipping duplicate candlestick for {Ticker} at {Ts}", ticker, candle.EndPeriodTs);
                    continue;
                }

                var record = new MarketCandlestickData
                {
                    Id = Guid.NewGuid(),
                    Ticker = ticker,
                    SeriesTicker = seriesTicker,
                    PeriodInterval = periodInterval,
                    EndPeriodTs = candle.EndPeriodTs,
                    EndPeriodTime = DateTimeOffset.FromUnixTimeSeconds(candle.EndPeriodTs).UtcDateTime,

                    // Yes Bid OHLC
                    YesBidOpen = candle.YesBid.Open,
                    YesBidLow = candle.YesBid.Low,
                    YesBidHigh = candle.YesBid.High,
                    YesBidClose = candle.YesBid.Close,
                    YesBidOpenDollars = candle.YesBid.OpenDollars,
                    YesBidLowDollars = candle.YesBid.LowDollars,
                    YesBidHighDollars = candle.YesBid.HighDollars,
                    YesBidCloseDollars = candle.YesBid.CloseDollars,

                    // Yes Ask OHLC
                    YesAskOpen = candle.YesAsk.Open,
                    YesAskLow = candle.YesAsk.Low,
                    YesAskHigh = candle.YesAsk.High,
                    YesAskClose = candle.YesAsk.Close,
                    YesAskOpenDollars = candle.YesAsk.OpenDollars,
                    YesAskLowDollars = candle.YesAsk.LowDollars,
                    YesAskHighDollars = candle.YesAsk.HighDollars,
                    YesAskCloseDollars = candle.YesAsk.CloseDollars,

                    // Price OHLC (nullable)
                    PriceOpen = candle.Price?.Open,
                    PriceLow = candle.Price?.Low,
                    PriceHigh = candle.Price?.High,
                    PriceClose = candle.Price?.Close,
                    PriceMean = candle.Price?.Mean,
                    PricePrevious = candle.Price?.Previous,
                    PriceOpenDollars = candle.Price?.OpenDollars,
                    PriceLowDollars = candle.Price?.LowDollars,
                    PriceHighDollars = candle.Price?.HighDollars,
                    PriceCloseDollars = candle.Price?.CloseDollars,
                    PriceMeanDollars = candle.Price?.MeanDollars,
                    PricePreviousDollars = candle.Price?.PreviousDollars,

                    Volume = candle.Volume,
                    OpenInterest = candle.OpenInterest,
                    FetchedAt = fetchedAt
                };

                newCandlestickRecords.Add(record);
            }

            // Bulk insert new records into database
            if (newCandlestickRecords.Any())
            {
                await _db.MarketCandlesticks.AddRangeAsync(newCandlestickRecords, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Saved {Count} new candlestick records for {Ticker}",
                    newCandlestickRecords.Count, ticker);
            }
            else
            {
                _logger.LogInformation("No new candlesticks to save for {Ticker}", ticker);
            }

            // Combine existing and new data for response
            var allData = existingData.Concat(newCandlestickRecords).ToList();

            return CreateResponseFromExistingData(ticker, allData);
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error fetching candlesticks for {Ticker}", ticker);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching chart data for {Ticker}", ticker);
            throw;
        }
    }

    /// <summary>
    /// Creates a ChartDataResponse from existing database records
    /// </summary>
    private ChartDataResponse CreateResponseFromExistingData(string ticker, List<MarketCandlestickData> data)
    {
        if (!data.Any())
        {
            return CreateEmptyResponse(ticker);
        }

        // Convert to response format using latest price (close) from each candle
        var dataPoints = data
            .OrderBy(c => c.EndPeriodTs)
            .Select(c => new ChartDataPoint
            {
                Timestamp = c.EndPeriodTime,
                // Use last price if available, otherwise use yes bid close
                Value = c.PriceClose ?? c.YesBidClose
            })
            .ToList();

        return new ChartDataResponse
        {
            Ticker = ticker,
            DataPoints = dataPoints
        };
    }

    private ChartDataResponse CreateEmptyResponse(string ticker)
    {
        return new ChartDataResponse
        {
            Ticker = ticker,
            DataPoints = new List<ChartDataPoint>()
        };
    }
}
