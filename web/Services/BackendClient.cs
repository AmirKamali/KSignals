using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using web_asp.Models;
using KSignals.DTO;

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

    public async Task<(bool Success, string? ErrorMessage, SignInResponse? Response)> SetUsernameAsync(string firebaseId, string username)
    {
        try
        {
            var request = new SetUsernameRequest
            {
                FirebaseId = firebaseId,
                Username = username
            };

            var url = $"{_options.BaseUrl.TrimEnd('/')}/api/users/setUsername";
            var content = new StringContent(
                JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    return (false, errorElement.GetString() ?? "Failed to set username", null);
                }
                return (false, "Failed to set username", null);
            }

            var signInResponse = JsonSerializer.Deserialize<SignInResponse>(responseBody, _jsonOptions);
            return (true, null, signInResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call setUsername API");
            return (false, "An error occurred. Please try again.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, SignInResponse? Response)> LoginAsync(
        string firebaseId,
        string? username,
        string? firstName,
        string? lastName,
        string? email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(firebaseId))
            {
                _logger.LogWarning("LoginAsync called with empty FirebaseId");
                return (false, "Firebase ID is required.", null);
            }

            var request = new SignInRequest
            {
                FirebaseId = firebaseId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                IsComnEmailOn = true
            };

            var url = $"{_options.BaseUrl.TrimEnd('/')}/api/users/login";
            _logger.LogDebug("LoginAsync: Calling {Url} for FirebaseId: {FirebaseId}", url, firebaseId);
            
            var content = new StringContent(
                JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug(
                "LoginAsync: Response status {StatusCode}, Body length: {BodyLength}",
                response.StatusCode, responseBody.Length);

            if (!response.IsSuccessStatusCode)
            {
                string? errorMessage = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        errorMessage = errorElement.GetString();
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "LoginAsync: Failed to parse error response as JSON. Body: {Body}", responseBody);
                }

                _logger.LogError(
                    "LoginAsync: Failed with status {StatusCode}. Error: {ErrorMessage}, Response body: {Body}",
                    response.StatusCode, errorMessage, responseBody);

                return (false, errorMessage ?? $"Failed to refresh session (Status: {response.StatusCode})", null);
            }

            try
            {
                var signInResponse = JsonSerializer.Deserialize<SignInResponse>(responseBody, _jsonOptions);
                if (signInResponse == null || string.IsNullOrWhiteSpace(signInResponse.Token))
                {
                    _logger.LogWarning("LoginAsync: Response missing token. Response body: {Body}", responseBody);
                    return (false, "Invalid response from server.", null);
                }

                _logger.LogDebug("LoginAsync: Successfully logged in. Username: {Username}, Token length: {TokenLength}", 
                    signInResponse.Username, signInResponse.Token.Length);
                return (true, null, signInResponse);
            }
            catch (Exception deserializeEx)
            {
                _logger.LogError(deserializeEx, "LoginAsync: Failed to deserialize response. Body: {Body}", responseBody);
                return (false, "Invalid response format from server.", null);
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "LoginAsync: HTTP request failed. URL: {Url}", $"{_options.BaseUrl.TrimEnd('/')}/api/users/login");
            return (false, "Unable to connect to the server. Please check your connection.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoginAsync: Unexpected error occurred");
            return (false, "Unable to refresh your session. Please sign in again.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, SignInResponse? Response)> UpdateNameAsync(
        string jwt,
        string firstName,
        string lastName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(jwt))
            {
                _logger.LogWarning("UpdateNameAsync called with empty JWT token");
                return (false, "Authentication token is missing.", null);
            }

            var request = new UpdateProfileRequest
            {
                FirstName = firstName,
                LastName = lastName
            };

            var url = $"{_options.BaseUrl.TrimEnd('/')}/api/users/profile";
            _logger.LogDebug("UpdateNameAsync: Calling {Url} with FirstName: {FirstName}, LastName: {LastName}", 
                url, firstName, lastName);
            
            using var message = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(request),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            using var response = await _httpClient.SendAsync(message);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug(
                "UpdateNameAsync: Response status {StatusCode}, Body length: {BodyLength}",
                response.StatusCode, responseBody.Length);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning(
                        "UpdateNameAsync: Unauthorized response. Status: {StatusCode}, Body: {Body}",
                        response.StatusCode, responseBody);
                    return (false, "Authentication expired. Please sign in again.", null);
                }

                string? errorMessage = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        errorMessage = errorElement.GetString();
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "UpdateNameAsync: Failed to parse error response as JSON. Body: {Body}", responseBody);
                }

                _logger.LogError(
                    "UpdateNameAsync: Failed with status {StatusCode}. Error: {ErrorMessage}, Response body: {Body}",
                    response.StatusCode, errorMessage, responseBody);

                return (false, errorMessage ?? $"Failed to update name (Status: {response.StatusCode})", null);
            }

            try
            {
                var signInResponse = JsonSerializer.Deserialize<SignInResponse>(responseBody, _jsonOptions);
                if (signInResponse == null)
                {
                    _logger.LogWarning("UpdateNameAsync: Deserialized response is null. Response body: {Body}", responseBody);
                    return (false, "Invalid response from server.", null);
                }

                _logger.LogDebug("UpdateNameAsync: Successfully updated name. Username: {Username}", signInResponse.Username);
                return (true, null, signInResponse);
            }
            catch (Exception deserializeEx)
            {
                _logger.LogError(deserializeEx, 
                    "UpdateNameAsync: Failed to deserialize response. Body: {Body}", responseBody);
                return (false, "Invalid response format from server.", null);
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "UpdateNameAsync: HTTP request failed. URL: {Url}", $"{_options.BaseUrl.TrimEnd('/')}/api/users/profile");
            return (false, "Unable to connect to the server. Please check your connection.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateNameAsync: Unexpected error occurred");
            return (false, "An error occurred. Please try again.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, UserProfileResponse? Response)> GetUserProfileAsync(string jwt)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(jwt))
            {
                _logger.LogWarning("GetUserProfileAsync called with empty JWT token");
                return (false, "Authentication token is missing.", null);
            }

            var url = $"{_options.BaseUrl.TrimEnd('/')}/api/users/me";
            _logger.LogDebug("GetUserProfileAsync: Calling {Url} with JWT token (length: {JwtLength})", url, jwt.Length);
            
            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            using var response = await _httpClient.SendAsync(message);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug(
                "GetUserProfileAsync: Response status {StatusCode}, Body length: {BodyLength}",
                response.StatusCode, responseBody.Length);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning(
                        "GetUserProfileAsync: Unauthorized response. Status: {StatusCode}, Body: {Body}",
                        response.StatusCode, responseBody);
                    return (false, "Authentication expired. Please sign in again.", null);
                }

                string? errorMessage = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        errorMessage = errorElement.GetString();
                    }
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "GetUserProfileAsync: Failed to parse error response as JSON. Body: {Body}", responseBody);
                }

                _logger.LogError(
                    "GetUserProfileAsync: Failed with status {StatusCode}. Error: {ErrorMessage}, Response body: {Body}",
                    response.StatusCode, errorMessage, responseBody);

                return (false, errorMessage ?? $"Failed to fetch profile (Status: {response.StatusCode})", null);
            }

            try
            {
                var profile = JsonSerializer.Deserialize<UserProfileResponse>(responseBody, _jsonOptions);
                if (profile == null)
                {
                    _logger.LogWarning("GetUserProfileAsync: Deserialized profile is null. Response body: {Body}", responseBody);
                    return (false, "Invalid response from server.", null);
                }
                
                _logger.LogDebug("GetUserProfileAsync: Successfully retrieved profile for user: {Username}", profile.Username);
                return (true, null, profile);
            }
            catch (Exception deserializeEx)
            {
                _logger.LogError(deserializeEx, 
                    "GetUserProfileAsync: Failed to deserialize response. Body: {Body}", responseBody);
                return (false, "Invalid response format from server.", null);
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "GetUserProfileAsync: HTTP request failed. URL: {Url}", $"{_options.BaseUrl.TrimEnd('/')}/api/users/me");
            return (false, "Unable to connect to the server. Please check your connection.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetUserProfileAsync: Unexpected error occurred");
            return (false, "An error occurred. Please try again.", null);
        }
    }

    public async Task<IReadOnlyList<ClientEvent>> GetHighVolumeMarketsAsync(int limit = 100)
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

    public async Task<ClientEvent?> GetMarketDetailsAsync(string tickerId)
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
                return JsonSerializer.Deserialize<ClientEvent>(marketNode.GetRawText(), _jsonOptions);
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
                $"{_options.BaseUrl.TrimEnd('/')}/api/events",
                BuildQuery(query));

            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Markets request failed with status {response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            var markets = new List<ClientEvent>();
            if (root.TryGetProperty("markets", out var marketsNode) && marketsNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in marketsNode.EnumerateArray())
                {
                    var market = JsonSerializer.Deserialize<ClientEvent>(entry.GetRawText(), _jsonOptions);
                    if (market != null)
                    {
                        markets.Add(market);
                    }
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

    private static IReadOnlyList<ClientEvent> SampleMarkets() => new List<ClientEvent>
    {
        new()
        {
            Ticker = "RATECUT-JUN25",
            EventTicker = "FOMC",
            SeriesTicker = "FED",
            Title = "Will the Fed cut rates by June 2025?",
            SubTitle = "Market signal: probability tightening then easing",
            Category = "Economics",
            YesBid = 42,
            NoBid = 58,
            LastPrice = 42,
            PreviousYesBid = 39,
            Volume = 182300,
            OpenInterest = 120000,
            Liquidity = 120000,
            Status = "open",
            CloseTime = DateTime.UtcNow.AddDays(30)
        },
        new()
        {
            Ticker = "ELECTION-TURNOUT",
            EventTicker = "ELECTIONS",
            SeriesTicker = "ELECTIONS",
            Title = "National election turnout above 60%",
            SubTitle = "Tracking early vote momentum",
            Category = "Politics",
            YesBid = 61,
            NoBid = 39,
            LastPrice = 61,
            PreviousYesBid = 63,
            Volume = 205500,
            OpenInterest = 153200,
            Liquidity = 153200,
            Status = "open",
            CloseTime = DateTime.UtcNow.AddDays(60)
        },
        new()
        {
            Ticker = "CPI-PRINT-JULY",
            EventTicker = "ECON",
            SeriesTicker = "CPI",
            Title = "July CPI YoY above 3.0%",
            SubTitle = "Inflation watchlist",
            Category = "Economics",
            YesBid = 34,
            NoBid = 66,
            LastPrice = 34,
            PreviousYesBid = 30,
            Volume = 120400,
            OpenInterest = 95400,
            Liquidity = 95400,
            Status = "open",
            CloseTime = DateTime.UtcNow.AddDays(20)
        },
        new()
        {
            Ticker = "AI-CHIP-EXPORTS",
            EventTicker = "TECH",
            SeriesTicker = "TECH",
            Title = "Will AI chip export rules tighten?",
            SubTitle = "Policy risk premium",
            Category = "Technology",
            YesBid = 28,
            NoBid = 72,
            LastPrice = 28,
            PreviousYesBid = 25,
            Volume = 84500,
            OpenInterest = 61200,
            Liquidity = 61200,
            Status = "open",
            CloseTime = DateTime.UtcNow.AddDays(15)
        },
        new()
        {
            Ticker = "GDP-Q3-ABOVE3",
            EventTicker = "ECON",
            SeriesTicker = "GDP",
            Title = "Will Q3 GDP growth exceed 3.0%?",
            SubTitle = "Macro expansion odds",
            Category = "Economics",
            YesBid = 47,
            NoBid = 53,
            LastPrice = 47,
            PreviousYesBid = 45,
            Volume = 91000,
            OpenInterest = 70000,
            Liquidity = 70000,
            Status = "open",
            CloseTime = DateTime.UtcNow.AddDays(80)
        },
        new()
        {
            Ticker = "SENATE-MAJORITY",
            EventTicker = "POL",
            SeriesTicker = "POLITICS",
            Title = "Will Party A hold the Senate majority?",
            SubTitle = "Seat map pressure",
            Category = "Politics",
            YesBid = 55,
            NoBid = 45,
            LastPrice = 55,
            PreviousYesBid = 52,
            Volume = 132000,
            OpenInterest = 88000,
            Liquidity = 88000,
            Status = "open",
            CloseTime = DateTime.UtcNow.AddDays(120)
        },
        new()
        {
            Ticker = "JOBLESS-CLAIMS",
            EventTicker = "LABOR",
            SeriesTicker = "LABOR",
            Title = "Weekly jobless claims above 250k?",
            SubTitle = "Volatility watchlist",
            Category = "Economics",
            YesBid = 23,
            NoBid = 77,
            LastPrice = 23,
            PreviousYesBid = 24,
            Volume = 56000,
            OpenInterest = 41000,
            Liquidity = 41000,
            Status = "open",
            CloseTime = DateTime.UtcNow.AddDays(7)
        }
    };
}
