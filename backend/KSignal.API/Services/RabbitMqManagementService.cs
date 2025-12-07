using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace KSignal.API.Services;

/// <summary>
/// Service for managing RabbitMQ queues via the HTTP Management API.
/// </summary>
public class RabbitMqManagementService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RabbitMqManagementService> _logger;
    private readonly string _managementBaseUrl;
    private readonly string _virtualHost;

    // Queue names matching MassTransit kebab-case convention
    private static readonly string[] QueueNames = new[]
    {
        "synchronize-market-data",
        "synchronize-tags-categories",
        "synchronize-series",
        "synchronize-events",
        "synchronize-event-detail",
        "synchronize-orderbook",
        "synchronize-candlesticks",
        "process-market-analytics",
        "cleanup-market-data"
    };

    public RabbitMqManagementService(
        IConfiguration configuration,
        ILogger<RabbitMqManagementService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var rabbitSection = configuration.GetSection("RabbitMq");
        var rabbitAddress = Environment.GetEnvironmentVariable("RABBITMQ_ADDRESS") ?? rabbitSection["Address"];
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? rabbitSection["Host"] ?? "localhost";
        var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? rabbitSection["Username"] ?? "guest";
        var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? rabbitSection["Password"] ?? "guest";
        var rabbitVirtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST") ?? rabbitSection["VirtualHost"] ?? "/";
        var managementPort = Environment.GetEnvironmentVariable("RABBITMQ_MANAGEMENT_PORT") ?? rabbitSection["ManagementPort"] ?? "15672";

        // Parse connection URI if provided
        if (!string.IsNullOrWhiteSpace(rabbitAddress) && Uri.TryCreate(rabbitAddress, UriKind.Absolute, out var uri))
        {
            rabbitHost = string.IsNullOrWhiteSpace(uri.Host) ? rabbitHost : uri.Host;
            
            if (!string.IsNullOrWhiteSpace(uri.AbsolutePath))
            {
                var vhost = uri.AbsolutePath.TrimStart('/');
                rabbitVirtualHost = string.IsNullOrWhiteSpace(vhost) ? "/" : vhost;
            }

            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                rabbitUser = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0] : rabbitUser;
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    rabbitPass = parts[1];
                }
            }
        }

        _managementBaseUrl = $"http://{rabbitHost}:{managementPort}";
        _virtualHost = rabbitVirtualHost;

        _httpClient = new HttpClient();
        var authBytes = Encoding.ASCII.GetBytes($"{rabbitUser}:{rabbitPass}");
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        _logger.LogInformation("RabbitMQ Management configured: {BaseUrl}, VHost: {VHost}", 
            _managementBaseUrl, _virtualHost);
    }

    /// <summary>
    /// Purges all MassTransit consumer queues, removing all pending messages.
    /// </summary>
    /// <returns>Result with counts of purged queues and any errors</returns>
    public async Task<PurgeResult> PurgeAllQueuesAsync(CancellationToken cancellationToken = default)
    {
        var result = new PurgeResult();
        var encodedVhost = Uri.EscapeDataString(_virtualHost);

        foreach (var queueName in QueueNames)
        {
            try
            {
                var url = $"{_managementBaseUrl}/api/queues/{encodedVhost}/{queueName}/contents";
                
                _logger.LogInformation("Purging queue: {QueueName}", queueName);
                
                var response = await _httpClient.DeleteAsync(url, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    result.PurgedQueues.Add(queueName);
                    _logger.LogInformation("Successfully purged queue: {QueueName}", queueName);
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // Queue doesn't exist (might not have been created yet)
                    result.SkippedQueues.Add(queueName);
                    _logger.LogWarning("Queue not found (may not exist yet): {QueueName}", queueName);
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    result.Errors.Add($"{queueName}: {response.StatusCode} - {errorBody}");
                    _logger.LogError("Failed to purge queue {QueueName}: {StatusCode} - {Error}", 
                        queueName, response.StatusCode, errorBody);
                }
            }
            catch (HttpRequestException ex)
            {
                result.Errors.Add($"{queueName}: Connection failed - {ex.Message}");
                _logger.LogError(ex, "HTTP error purging queue {QueueName}", queueName);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                result.Errors.Add($"{queueName}: Request timed out");
                _logger.LogError(ex, "Timeout purging queue {QueueName}", queueName);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the current message count for all MassTransit consumer queues.
    /// </summary>
    public async Task<Dictionary<string, QueueInfo>> GetQueueStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new Dictionary<string, QueueInfo>();
        var encodedVhost = Uri.EscapeDataString(_virtualHost);

        foreach (var queueName in QueueNames)
        {
            try
            {
                var url = $"{_managementBaseUrl}/api/queues/{encodedVhost}/{queueName}";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var queueData = System.Text.Json.JsonSerializer.Deserialize<QueueApiResponse>(json);
                    
                    stats[queueName] = new QueueInfo
                    {
                        MessageCount = queueData?.Messages ?? 0,
                        ConsumerCount = queueData?.Consumers ?? 0,
                        MessagesReady = queueData?.MessagesReady ?? 0,
                        MessagesUnacknowledged = queueData?.MessagesUnacknowledged ?? 0,
                        Exists = true
                    };
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    stats[queueName] = new QueueInfo { Exists = false };
                }
                else
                {
                    stats[queueName] = new QueueInfo { Exists = false, Error = response.StatusCode.ToString() };
                }
            }
            catch (Exception ex)
            {
                stats[queueName] = new QueueInfo { Exists = false, Error = ex.Message };
                _logger.LogWarning(ex, "Error getting stats for queue {QueueName}", queueName);
            }
        }

        return stats;
    }

    private class QueueApiResponse
    {
        public int Messages { get; set; }
        public int Consumers { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("messages_ready")]
        public int MessagesReady { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("messages_unacknowledged")]
        public int MessagesUnacknowledged { get; set; }
    }
}

public class PurgeResult
{
    public List<string> PurgedQueues { get; set; } = new();
    public List<string> SkippedQueues { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
}

public class QueueInfo
{
    public bool Exists { get; set; }
    public int MessageCount { get; set; }
    public int ConsumerCount { get; set; }
    public int MessagesReady { get; set; }
    public int MessagesUnacknowledged { get; set; }
    public string? Error { get; set; }
}

