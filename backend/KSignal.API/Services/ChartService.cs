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
    /// Gets chart data for a market ticker, fetching from Kalshi API and storing in database
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

            // Calculate time range: last 30 days
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddDays(-30);
            var startTs = ((DateTimeOffset)startTime).ToUnixTimeSeconds();
            var endTs = ((DateTimeOffset)endTime).ToUnixTimeSeconds();

            // Fetch candlestick data from Kalshi API (1 day interval)
            var candlesticksResponse = await _kalshiClient.Markets.GetMarketCandlesticksAsync(
                seriesTicker: seriesTicker,
                ticker: ticker,
                startTs: startTs,
                endTs: endTs,
                periodInterval: 1440, // 1 day
                cancellationToken: cancellationToken
            );

            if (candlesticksResponse?.Candlesticks == null || !candlesticksResponse.Candlesticks.Any())
            {
                _logger.LogWarning("No candlestick data returned for {Ticker}", ticker);
                return CreateEmptyResponse(ticker);
            }

            _logger.LogInformation("Received {Count} candlesticks for {Ticker}",
                candlesticksResponse.Candlesticks.Count, ticker);

            // Save candlesticks to database
            var fetchedAt = DateTime.UtcNow;
            var candlestickRecords = new List<MarketCandlestickData>();

            foreach (var candle in candlesticksResponse.Candlesticks)
            {
                var record = new MarketCandlestickData
                {
                    Id = Guid.NewGuid(),
                    Ticker = ticker,
                    SeriesTicker = seriesTicker,
                    PeriodInterval = 1440,
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

                candlestickRecords.Add(record);
            }

            // Bulk insert into database
            await _db.MarketCandlesticks.AddRangeAsync(candlestickRecords, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Saved {Count} candlestick records for {Ticker}",
                candlestickRecords.Count, ticker);

            // Convert to response format using latest price (close) from each candle
            var dataPoints = candlesticksResponse.Candlesticks
                .Select(c => new ChartDataPoint
                {
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(c.EndPeriodTs).UtcDateTime,
                    // Use last price if available, otherwise use yes bid close
                    Value = c.Price?.Close ?? c.YesBid.Close
                })
                .OrderBy(dp => dp.Timestamp)
                .ToList();

            return new ChartDataResponse
            {
                Ticker = ticker,
                DataPoints = dataPoints
            };
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

    private ChartDataResponse CreateEmptyResponse(string ticker)
    {
        return new ChartDataResponse
        {
            Ticker = ticker,
            DataPoints = new List<ChartDataPoint>()
        };
    }
}
