using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Messaging;
using KSignal.API.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KSignal.API.Services;

public class SynchronizationService
{
    private readonly KalshiClient _kalshiClient;
    private readonly KalshiDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<SynchronizationService> _logger;

    public SynchronizationService(
        KalshiClient kalshiClient,
        KalshiDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<SynchronizationService> logger)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnqueueMarketSyncAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Queueing market synchronization (cursor={Cursor})", cursor ?? "<start>");
            await _publishEndpoint.Publish(new SynchronizeMarketData(cursor), cancellationToken);
        }
        catch (Exception ex)
        {
            // Re-throw other exceptions as-is
            _logger.LogError(ex, "Unexpected error while trying to enqueue synchronization. Exception type: {ExceptionType}", exceptionTypeName);
            throw;
        }
    }

    public async Task SynchronizeMarketDataAsync(SynchronizeMarketData command, CancellationToken cancellationToken)
    {
        var request = BuildRequest(command.Cursor);
        var response = await _kalshiClient.Markets.AsynchronousClient.GetAsync<GetMarketsResponse>(
            "/markets",
            request,
            _kalshiClient.Markets.Configuration,
            cancellationToken);

        var payload = response?.Data ?? new GetMarketsResponse();
        var fetchedAt = DateTime.UtcNow;
        _logger.LogInformation("Fetched {Count} markets from Kalshi.API (cursor={Cursor})", payload.Markets.Count, command.Cursor ?? "<start>");

        var mapped = payload.Markets
            .Where(m => m != null)
            .Select(m =>
            {
                var seriesKey = string.IsNullOrWhiteSpace(m.EventTicker) ? m.Ticker : m.EventTicker;
                return KalshiService.MapMarket(seriesKey ?? m.Ticker, m, fetchedAt);
            })
            .ToList();

        await UpsertMarketsAsync(mapped, cancellationToken);

        if (!string.IsNullOrWhiteSpace(payload.Cursor))
        {
            _logger.LogInformation("Queueing next market sync page (cursor={Cursor})", payload.Cursor);
            await _publishEndpoint.Publish(new SynchronizeMarketData(payload.Cursor), cancellationToken);
        }
    }

    private static RequestOptions BuildRequest(string? cursor)
    {
        var requestOptions = new RequestOptions
        {
            Operation = "MarketApi.GetMarkets"
        };

        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "limit", 250));
        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "status", "open"));
        requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "with_nested_markets", true));

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            requestOptions.QueryParameters.Add(ClientUtils.ParameterToMultiMap("", "cursor", cursor));
        }

        return requestOptions;
    }

    private async Task UpsertMarketsAsync(IReadOnlyCollection<MarketCache> markets, CancellationToken cancellationToken)
    {
        if (markets.Count == 0)
        {
            _logger.LogInformation("No markets to upsert for this page");
            return;
        }

        var tickers = markets.Select(m => m.TickerId).ToList();
        var existing = await _dbContext.Markets
            .Where(m => tickers.Contains(m.TickerId))
            .ToListAsync(cancellationToken);

        var lookup = existing.ToDictionary(m => m.TickerId, StringComparer.OrdinalIgnoreCase);

        foreach (var market in markets)
        {
            if (lookup.TryGetValue(market.TickerId, out var current))
            {
                _dbContext.Entry(current).CurrentValues.SetValues(market);
            }
            else
            {
                _dbContext.Markets.Add(market);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Upserted {Count} markets into cache", markets.Count);
    }
}
