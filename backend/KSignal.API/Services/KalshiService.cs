using Kalshi.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Model;
using KSignal.API.Data;
using KSignal.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KSignal.API.Services;

public class KalshiService
{
    private readonly KalshiClient _kalshiClient;
    private readonly ILogger<KalshiService> _logger;
    private readonly KalshiDbContext _db;

    public KalshiService(KalshiClient kalshiClient, ILogger<KalshiService> logger, KalshiDbContext db, IConfiguration configuration)
    {
        _kalshiClient = kalshiClient ?? throw new ArgumentNullException(nameof(kalshiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _db = db ?? throw new ArgumentNullException(nameof(db));

        var connectionString = configuration.GetConnectionString("KalshiMySql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'KalshiMySql' is missing.");
        }
    }

    public async Task<int> RefreshMarketCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);

        var existingIds = (await _db.MarketCategories
                .AsNoTracking()
                .Select(x => x.SeriesId)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tagsResponse = await _kalshiClient.Search.GetTagsForSeriesCategoriesAsync(cancellationToken: cancellationToken);
        if (tagsResponse?.TagsByCategories == null)
        {
            _logger.LogWarning("Tags by categories response was null or empty.");
            return 0;
        }

        _logger.LogInformation("Fetched {CategoryCount} categories from tags endpoint", tagsResponse.TagsByCategories.Count);
        var now = DateTime.UtcNow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pull series per category
        foreach (var category in tagsResponse.TagsByCategories.Keys)
        {
            _logger.LogInformation("Fetching series for category {Category}", category);
            await FetchSeriesIntoLookupAsync(existingIds, seen, now, category, null, cancellationToken);
        }

        // Pull series per tag
        foreach (var kvp in tagsResponse.TagsByCategories)
        {
            if (kvp.Value == null) continue;
            foreach (var tag in kvp.Value)
            {
                _logger.LogInformation("Fetching series for tag {Tag}", tag);
                await FetchSeriesIntoLookupAsync(existingIds, seen, now, null, tag, cancellationToken);
            }
        }

        await CleanupMissingAsync(existingIds, cancellationToken);
        return seen.Count;
    }

    private async Task FetchSeriesIntoLookupAsync(
        HashSet<string> existingIds,
        HashSet<string> seen,
        DateTime lastUpdate,
        string? category,
        string? tag,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _kalshiClient.Markets.GetSeriesListAsync(
                category: category,
                tags: tag,
                includeProductMetadata: true,
                cancellationToken: cancellationToken);

            if (response?.Series == null)
            {
                _logger.LogWarning("Series list response was null (category={Category}, tag={Tag})", category, tag);
                return;
            }

            foreach (var series in response.Series)
            {
                if (series == null || string.IsNullOrWhiteSpace(series.Ticker)) continue;

                var agg = new SeriesAggregation(series.Ticker);
                agg.Merge(series);
                var record = agg.ToRecord(lastUpdate);
                await UpsertSeriesAsync(record, cancellationToken);
                seen.Add(series.Ticker);
                existingIds.Remove(series.Ticker);
            }
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(apiEx, "Kalshi API error when fetching series (category={Category}, tag={Tag})", category, tag);
            throw;
        }
    }

    private async Task UpsertSeriesAsync(MarketCategoryRecord record, CancellationToken cancellationToken)
    {
        var existing = await _db.MarketCategories.FindAsync(new object[] { record.SeriesId }, cancellationToken);
        if (existing == null)
        {
            _db.MarketCategories.Add(new MarketCategory
            {
                SeriesId = record.SeriesId,
                Category = record.Category,
                Tags = record.Tags,
                Ticker = record.Ticker,
                Title = record.Title,
                Frequency = record.Frequency,
                JsonResponse = record.JsonResponse,
                LastUpdate = record.LastUpdate
            });
        }
        else
        {
            existing.Category = record.Category;
            existing.Tags = record.Tags;
            existing.Ticker = record.Ticker;
            existing.Title = record.Title;
            existing.Frequency = record.Frequency;
            existing.JsonResponse = record.JsonResponse;
            existing.LastUpdate = record.LastUpdate;

            _db.MarketCategories.Update(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task CleanupMissingAsync(HashSet<string> remainingIds, CancellationToken cancellationToken)
    {
        if (remainingIds.Count == 0) return;

        var toRemove = await _db.MarketCategories
            .Where(x => remainingIds.Contains(x.SeriesId))
            .ToListAsync(cancellationToken);

        if (toRemove.Count == 0) return;

        _db.MarketCategories.RemoveRange(toRemove);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private sealed class SeriesAggregation
    {
        public SeriesAggregation(string ticker)
        {
            Ticker = ticker;
        }

        public string Ticker { get; }
        public string? Category { get; private set; }
        public string? Title { get; private set; }
        public string? Frequency { get; private set; }
        public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Series? LatestRaw { get; private set; }

        public void Merge(Series series)
        {
            Category ??= series.Category;
            Title ??= series.Title;
            Frequency ??= series.Frequency;
            foreach (var tag in series.Tags ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    Tags.Add(tag);
                }
            }

            LatestRaw = series;
        }

        public MarketCategoryRecord ToRecord(DateTime lastUpdate)
        {
            var tagsJoined = string.Join(",", Tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase));
            return new MarketCategoryRecord
            {
                SeriesId = Ticker,
                Category = Category,
                Tags = tagsJoined,
                Ticker = Ticker,
                Title = Title,
                Frequency = Frequency,
                JsonResponse = JsonConvert.SerializeObject(LatestRaw),
                LastUpdate = lastUpdate
            };
        }
    }

    private sealed class MarketCategoryRecord
    {
        public string SeriesId { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Tags { get; set; }
        public string? Ticker { get; set; }
        public string? Title { get; set; }
        public string? Frequency { get; set; }
        public string JsonResponse { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
    }
}
