using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using web_asp.Models;

namespace web_asp.Services;

public class BackendClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BackendClient> _logger;
    private readonly BackendOptions _options;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public BackendClient(HttpClient httpClient, IOptions<BackendOptions> options, ILogger<BackendClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Market>> GetHighVolumeMarketsAsync(int limit = 100)
    {
        var response = await GetBackendMarketsAsync(new MarketQuery { PageSize = limit });
        var ordered = response.Markets.OrderByDescending(m => m.Volume).Take(limit).ToList();
        if (ordered.Count > 0)
        {
            return ordered;
        }

        return SampleMarkets();
    }

    public async Task<Dictionary<string, List<string>>> GetTagsByCategoriesAsync()
    {
        try
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}/api/events/categories";
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Tags request failed with status {response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            if (root.TryGetProperty("tagsByCategories", out var tagsNode) || root.TryGetProperty("tags_by_categories", out tagsNode))
            {
                return DeserializeTags(tagsNode);
            }

            _logger.LogWarning("Tags response missing expected payload, using defaults");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to default tags");
        }

        return DefaultTags();
    }

    public async Task<MarketDetails?> GetMarketDetailsAsync(string tickerId)
    {
        if (string.IsNullOrWhiteSpace(tickerId))
        {
            throw new ArgumentException("tickerId is required", nameof(tickerId));
        }

        try
        {
            var url = QueryHelpers.AddQueryString(
                $"{_options.BaseUrl.TrimEnd('/')}/api/marketDetails",
                new Dictionary<string, string?> { ["tickerId"] = tickerId });

            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Market details request failed with status {response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            if (root.TryGetProperty("market", out var marketNode))
            {
                return NormalizeMarketDetails(marketNode);
            }

            _logger.LogWarning("Market details response missing 'market' payload for ticker {TickerId}", tickerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch market details for ticker {TickerId}", tickerId);
        }

        return null;
    }

    public async Task<BackendMarketsResponse> GetBackendMarketsAsync(MarketQuery query)
    {
        try
        {
            var url = QueryHelpers.AddQueryString(
                $"{_options.BaseUrl.TrimEnd('/')}/api/markets",
                BuildQuery(query));

            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Markets request failed with status {response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            var markets = new List<Market>();
            if (root.TryGetProperty("markets", out var marketsNode) && marketsNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in marketsNode.EnumerateArray())
                {
                    markets.Add(NormalizeMarket(entry));
                }
            }

            return new BackendMarketsResponse
            {
                Markets = markets,
                TotalPages = GetInt(root, "totalPages"),
                TotalCount = GetInt(root, "count", "totalCount"),
                CurrentPage = GetInt(root, query.Page, "currentPage", "page", "pageNumber"),
                PageSize = GetInt(root, query.PageSize, "pageSize"),
                Sort = GetString(root, "sort_type", "sort") ?? query.Sort,
                Direction = GetString(root, "direction") ?? query.Direction
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to sample markets");
            var fallback = SampleMarkets();
            return new BackendMarketsResponse
            {
                Markets = fallback.ToList(),
                TotalPages = 1,
                TotalCount = fallback.Count,
                CurrentPage = 1,
                PageSize = fallback.Count,
                Sort = query.Sort,
                Direction = query.Direction
            };
        }
    }

    private static Dictionary<string, string?> BuildQuery(MarketQuery query)
    {
        var dict = new Dictionary<string, string?>();

        if (!string.IsNullOrWhiteSpace(query.Category) && query.Category != "All")
            dict["category"] = query.Category;
        if (!string.IsNullOrWhiteSpace(query.Tag))
            dict["tag"] = query.Tag;
        if (!string.IsNullOrWhiteSpace(query.CloseDateType))
            dict["close_date_type"] = query.CloseDateType;
        if (!string.IsNullOrWhiteSpace(query.Query))
            dict["query"] = query.Query;

        dict["sort_type"] = query.Sort;
        dict["direction"] = query.Direction;
        if (query.Page > 1)
            dict["page"] = query.Page.ToString();
        dict["pageSize"] = query.PageSize.ToString();

        return dict;
    }

    private static Dictionary<string, List<string>> DeserializeTags(JsonElement tagsNode)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in tagsNode.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                result[property.Name] = property.Value.EnumerateArray()
                    .Where(v => v.ValueKind == JsonValueKind.String)
                    .Select(v => v.GetString() ?? string.Empty)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();
            }
        }
        return result;
    }

    private static Dictionary<string, List<string>> DefaultTags() => new()
    {
        { "Economics", new List<string> { "Interest Rates", "Inflation", "GDP" } },
        { "Politics", new List<string> { "Elections", "Policy" } },
        { "Technology", new List<string> { "AI", "Hardware" } },
        { "Other", new List<string>() }
    };

    private static Market NormalizeMarket(JsonElement raw)
    {
        var ticker = GetString(raw, "tickerId", "ticker") ?? string.Empty;
        var seriesTicker = GetString(raw, "seriesTicker", "eventTicker", "event_ticker") ?? ticker;

        var lastPrice = GetDecimal(raw, "lastPrice");
        var noBid = GetDecimal(raw, "noBid", "no_price", "noPrice");
        var noAsk = GetDecimal(raw, "noAsk", "no_ask");
        var noPrice = noBid ?? noAsk;
        var yesBid = GetDecimal(raw, "yesBid", "yes_price", "yesPrice");

        var yesPrice = lastPrice ?? yesBid ?? (noPrice.HasValue ? Math.Max(0m, 100m - noPrice.Value) : 0m);
        var previousYesPrice = GetDecimal(raw, "previousPrice", "previousYesBid", "previous_yes_price", "previous_yes_bid");
        var previousNoPrice = GetDecimal(raw, "previousNoBid", "previous_no_price", "previous_no_bid") ??
                              (previousYesPrice.HasValue ? 100m - previousYesPrice.Value : null);

        var liquidity = GetDecimal(raw, "liquidity") ?? 0m;
        var volume = GetDecimal(raw, "volume") ?? 0m;
        var closeTime = GetString(raw, "closeTime", "close_time");

        return new Market
        {
            Ticker = ticker,
            EventTicker = seriesTicker ?? string.Empty,
            Title = GetString(raw, "title") ?? string.Empty,
            Subtitle = GetString(raw, "subtitle"),
            YesPrice = yesPrice,
            NoPrice = noPrice ?? (yesPrice > 0 ? Math.Max(0m, 100m - yesPrice) : null),
            PreviousYesPrice = previousYesPrice,
            PreviousNoPrice = previousNoPrice,
            Volume = volume,
            OpenInterest = liquidity,
            Liquidity = liquidity,
            Status = GetString(raw, "status") ?? string.Empty,
            Category = GetString(raw, "category") ?? seriesTicker,
            CloseTime = closeTime
        };
    }

    private static MarketDetails NormalizeMarketDetails(JsonElement raw)
    {
        var market = NormalizeMarket(raw);

        return new MarketDetails
        {
            Ticker = market.Ticker,
            EventTicker = market.EventTicker,
            Title = market.Title,
            Subtitle = market.Subtitle,
            YesPrice = market.YesPrice,
            NoPrice = market.NoPrice,
            PreviousYesPrice = market.PreviousYesPrice,
            PreviousNoPrice = market.PreviousNoPrice,
            Volume = market.Volume,
            OpenInterest = market.OpenInterest,
            Liquidity = market.Liquidity,
            Status = market.Status,
            Category = market.Category,
            CloseTime = market.CloseTime,
            YesBid = GetDecimal(raw, "yesBid", "yes_bid", "yesPrice"),
            YesAsk = GetDecimal(raw, "yesAsk", "yes_ask"),
            NoBid = GetDecimal(raw, "noBid", "no_bid", "noPrice"),
            NoAsk = GetDecimal(raw, "noAsk", "no_ask"),
            LastPrice = GetDecimal(raw, "lastPrice", "last_price"),
            Volume24h = GetDecimal(raw, "volume24h", "volume_24h", "24h_volume"),
            OpenTime = GetString(raw, "openTime", "open_time"),
            ExpirationTime = GetString(raw, "expirationTime", "expiration_time"),
            LatestExpirationTime = GetString(raw, "latestExpirationTime", "latest_expiration_time")
        };
    }

    private static decimal? GetDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    private static int GetInt(JsonElement element, params string[] names)
        => GetInt(element, 0, names);

    private static int GetInt(JsonElement element, int defaultValue, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (value.TryGetInt32(out var num)) return num;
                    break;
                case JsonValueKind.String when int.TryParse(value.GetString(), out var parsed):
                    return parsed;
            }
        }
        return defaultValue;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.ToString();
            }
        }
        return null;
    }

    private static IReadOnlyList<Market> SampleMarkets() => new List<Market>
    {
        new()
        {
            Ticker = "RATECUT-JUN25",
            EventTicker = "FOMC",
            Title = "Will the Fed cut rates by June 2025?",
            Subtitle = "Market signal: probability tightening then easing",
            YesPrice = 42,
            NoPrice = 58,
            PreviousYesPrice = 39,
            Volume = 182300,
            OpenInterest = 120000,
            Liquidity = 120000,
            Status = "open",
            Category = "Economics",
            CloseTime = DateTime.UtcNow.AddDays(30).ToString("o")
        },
        new()
        {
            Ticker = "ELECTION-TURNOUT",
            EventTicker = "ELECTIONS",
            Title = "National election turnout above 60%",
            Subtitle = "Tracking early vote momentum",
            YesPrice = 61,
            NoPrice = 39,
            PreviousYesPrice = 63,
            Volume = 205500,
            OpenInterest = 153200,
            Liquidity = 153200,
            Status = "open",
            Category = "Politics",
            CloseTime = DateTime.UtcNow.AddDays(60).ToString("o")
        },
        new()
        {
            Ticker = "CPI-PRINT-JULY",
            EventTicker = "ECON",
            Title = "July CPI YoY above 3.0%",
            Subtitle = "Inflation watchlist",
            YesPrice = 34,
            NoPrice = 66,
            PreviousYesPrice = 30,
            Volume = 120400,
            OpenInterest = 95400,
            Liquidity = 95400,
            Status = "open",
            Category = "Economics",
            CloseTime = DateTime.UtcNow.AddDays(20).ToString("o")
        },
        new()
        {
            Ticker = "AI-CHIP-EXPORTS",
            EventTicker = "TECH",
            Title = "Will AI chip export rules tighten?",
            Subtitle = "Policy risk premium",
            YesPrice = 28,
            NoPrice = 72,
            PreviousYesPrice = 25,
            Volume = 84500,
            OpenInterest = 61200,
            Liquidity = 61200,
            Status = "open",
            Category = "Technology",
            CloseTime = DateTime.UtcNow.AddDays(15).ToString("o")
        },
        new()
        {
            Ticker = "GDP-Q3-ABOVE3",
            EventTicker = "ECON",
            Title = "Will Q3 GDP growth exceed 3.0%?",
            Subtitle = "Macro expansion odds",
            YesPrice = 47,
            NoPrice = 53,
            PreviousYesPrice = 45,
            Volume = 91000,
            OpenInterest = 70000,
            Liquidity = 70000,
            Status = "open",
            Category = "Economics",
            CloseTime = DateTime.UtcNow.AddDays(80).ToString("o")
        },
        new()
        {
            Ticker = "SENATE-MAJORITY",
            EventTicker = "POL",
            Title = "Will Party A hold the Senate majority?",
            Subtitle = "Seat map pressure",
            YesPrice = 55,
            NoPrice = 45,
            PreviousYesPrice = 52,
            Volume = 132000,
            OpenInterest = 88000,
            Liquidity = 88000,
            Status = "open",
            Category = "Politics",
            CloseTime = DateTime.UtcNow.AddDays(120).ToString("o")
        },
        new()
        {
            Ticker = "JOBLESS-CLAIMS",
            EventTicker = "LABOR",
            Title = "Weekly jobless claims above 250k?",
            Subtitle = "Volatility watchlist",
            YesPrice = 23,
            NoPrice = 77,
            PreviousYesPrice = 24,
            Volume = 56000,
            OpenInterest = 41000,
            Liquidity = 41000,
            Status = "open",
            Category = "Economics",
            CloseTime = DateTime.UtcNow.AddDays(7).ToString("o")
        }
    };
}
