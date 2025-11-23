using Kalshi.Api.Api;
using Kalshi.Api.Client;
using Kalshi.Api.Configuration;

namespace Kalshi.Api
{
    /// <summary>
    /// Main client class for interacting with the Kalshi Trade API.
    /// Provides easy access to all API endpoints organized by category.
    /// </summary>
    /// <remarks>
    /// This class serves as a wrapper around the generated API clients, providing
    /// a unified interface for accessing all Kalshi API functionality.
    /// 
    /// For detailed API documentation, visit: https://docs.kalshi.com/
    /// </remarks>
    public class KalshiClient : IDisposable
    {
        private readonly Client.Configuration _configuration;
        private readonly Authentication.KalshiAuthenticationHandler? _authHandler;
        private bool _disposed = false;

        /// <summary>
        /// Gets the Exchange API client for exchange status and information endpoints.
        /// </summary>
        /// <remarks>
        /// Provides access to endpoints such as:
        /// - Get Exchange Status: https://docs.kalshi.com/api-reference/exchange/get-exchange-status
        /// - Get Exchange Announcements: https://docs.kalshi.com/api-reference/exchange/get-exchange-announcements
        /// - Get Exchange Schedule: https://docs.kalshi.com/api-reference/exchange/get-exchange-schedule
        /// </remarks>
        public ExchangeApi Exchange { get; }

        /// <summary>
        /// Gets the Portfolio API client for portfolio and balance information endpoints.
        /// </summary>
        /// <remarks>
        /// Provides access to endpoints such as:
        /// - Get Balance: https://docs.kalshi.com/api-reference/portfolio/get-balance
        /// - Get Positions: https://docs.kalshi.com/api-reference/portfolio/get-positions
        /// - Get Orders: https://docs.kalshi.com/api-reference/portfolio/get-orders
        /// </remarks>
        public PortfolioApi Portfolio { get; }

        /// <summary>
        /// Gets the Orders API client for order management endpoints.
        /// </summary>
        /// <remarks>
        /// Provides access to endpoints such as:
        /// - Create Order: https://docs.kalshi.com/api-reference/orders/create-order
        /// - Cancel Order: https://docs.kalshi.com/api-reference/orders/cancel-order
        /// - Amend Order: https://docs.kalshi.com/api-reference/orders/amend-order
        /// </remarks>
        public OrdersApi Orders { get; }

        /// <summary>
        /// Gets the Markets API client for market data endpoints.
        /// </summary>
        /// <remarks>
        /// Provides access to endpoints such as:
        /// - Get Orderbook: https://docs.kalshi.com/api-reference/markets/get-orderbook
        /// - Get Trades: https://docs.kalshi.com/api-reference/markets/get-trades
        /// </remarks>
        public MarketApi Markets { get; }

        /// <summary>
        /// Gets the Events API client for event endpoints.
        /// </summary>
        /// <remarks>
        /// Provides access to endpoints such as:
        /// - Get Events: https://docs.kalshi.com/api-reference/events/get-events
        /// - Get Event: https://docs.kalshi.com/api-reference/events/get-event
        /// </remarks>
        public EventsApi Events { get; }

        /// <summary>
        /// Gets the Search API client for search and filtering endpoints.
        /// </summary>
        public SearchApi Search { get; }

        /// <summary>
        /// Gets the Communications API client for request-for-quote (RFQ) endpoints.
        /// </summary>
        public CommunicationsApi Communications { get; }

        /// <summary>
        /// Gets the Multivariate API client for multivariate event collection endpoints.
        /// </summary>
        public MultivariateApi Multivariate { get; }

        /// <summary>
        /// Gets the Live Data API client for live data endpoints.
        /// </summary>
        public LiveDataApi LiveData { get; }

        /// <summary>
        /// Gets the Milestone API client for milestone endpoints.
        /// </summary>
        public MilestoneApi Milestones { get; }

        /// <summary>
        /// Gets the API Keys API client for API key management endpoints.
        /// </summary>
        public ApiKeysApi ApiKeys { get; }

        /// <summary>
        /// Gets the FCM API client for FCM member specific endpoints.
        /// </summary>
        public FcmApi Fcm { get; }

        /// <summary>
        /// Gets the Incentive Programs API client for incentive program endpoints.
        /// </summary>
        public IncentiveProgramsApi IncentivePrograms { get; }

        /// <summary>
        /// Gets the Structured Targets API client for structured targets endpoints.
        /// </summary>
        public StructuredTargetsApi StructuredTargets { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KalshiClient"/> class without authentication.
        /// Use this constructor for public endpoints that don't require authentication.
        /// </summary>
        /// <param name="baseUrl">Optional base URL for the API. Defaults to production server.</param>
        public KalshiClient(string? baseUrl = null)
        {
            _configuration = new Client.Configuration
            {
                BasePath = baseUrl ?? "https://api.elections.kalshi.com/trade-api/v2"
            };

            Exchange = new ExchangeApi(_configuration);
            Portfolio = new PortfolioApi(_configuration);
            Orders = new OrdersApi(_configuration);
            Markets = new MarketApi(_configuration);
            Events = new EventsApi(_configuration);
            Search = new SearchApi(_configuration);
            Communications = new CommunicationsApi(_configuration);
            Multivariate = new MultivariateApi(_configuration);
            LiveData = new LiveDataApi(_configuration);
            Milestones = new MilestoneApi(_configuration);
            ApiKeys = new ApiKeysApi(_configuration);
            Fcm = new FcmApi(_configuration);
            IncentivePrograms = new IncentiveProgramsApi(_configuration);
            StructuredTargets = new StructuredTargetsApi(_configuration);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KalshiClient"/> class with authentication.
        /// Use this constructor for authenticated endpoints that require API credentials.
        /// </summary>
        /// <param name="config">Configuration containing API credentials and settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
        /// <exception cref="ArgumentException">Thrown when config is invalid.</exception>
        public KalshiClient(KalshiApiConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (!config.IsValid())
                throw new ArgumentException("Configuration must include ApiKey and PrivateKey for authenticated requests.", nameof(config));

            _configuration = new Client.Configuration
            {
                BasePath = config.BaseUrl
            };

            _authHandler = new Authentication.KalshiAuthenticationHandler(config.ApiKey!, config.PrivateKey!);

            Exchange = new ExchangeApi(_configuration);
            Portfolio = new PortfolioApi(_configuration);
            Orders = new OrdersApi(_configuration);
            Markets = new MarketApi(_configuration);
            Events = new EventsApi(_configuration);
            Search = new SearchApi(_configuration);
            Communications = new CommunicationsApi(_configuration);
            Multivariate = new MultivariateApi(_configuration);
            LiveData = new LiveDataApi(_configuration);
            Milestones = new MilestoneApi(_configuration);
            ApiKeys = new ApiKeysApi(_configuration);
            Fcm = new FcmApi(_configuration);
            IncentivePrograms = new IncentiveProgramsApi(_configuration);
            StructuredTargets = new StructuredTargetsApi(_configuration);
        }

        /// <summary>
        /// Disposes of the client and releases any resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _authHandler?.Dispose();
                _disposed = true;
            }
        }
    }
}

