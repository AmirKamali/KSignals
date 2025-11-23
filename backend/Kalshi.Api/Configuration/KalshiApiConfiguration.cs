namespace Kalshi.Api.Configuration
{
    /// <summary>
    /// Configuration settings for the Kalshi API client.
    /// </summary>
    public class KalshiApiConfiguration
    {
        /// <summary>
        /// Gets or sets the base URL for the Kalshi API.
        /// Defaults to the production server: https://api.elections.kalshi.com/trade-api/v2
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.elections.kalshi.com/trade-api/v2";

        /// <summary>
        /// Gets or sets the API key ID (KALSHI-ACCESS-KEY).
        /// This is your API key identifier provided by Kalshi.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the private key used for generating RSA-PSS signatures.
        /// This should be the private key corresponding to your API key.
        /// </summary>
        public string? PrivateKey { get; set; }

        /// <summary>
        /// Gets or sets the timeout for API requests in milliseconds.
        /// Defaults to 30000 (30 seconds).
        /// </summary>
        public int Timeout { get; set; } = 30000;

        /// <summary>
        /// Validates that the configuration has the required credentials.
        /// </summary>
        /// <returns>True if the configuration is valid, false otherwise.</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(PrivateKey);
        }
    }
}

