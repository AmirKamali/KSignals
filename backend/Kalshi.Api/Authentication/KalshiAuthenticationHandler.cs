using System.Security.Cryptography;
using System.Text;
using RestSharp;

namespace Kalshi.Api.Authentication
{
    /// <summary>
    /// Handles authentication for Kalshi API requests by generating RSA-PSS signatures
    /// and adding required authentication headers.
    /// </summary>
    public class KalshiAuthenticationHandler
    {
        private readonly string _apiKey;
        private readonly RSA _privateKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="KalshiAuthenticationHandler"/> class.
        /// </summary>
        /// <param name="apiKey">The API key ID (KALSHI-ACCESS-KEY).</param>
        /// <param name="privateKeyPem">The private key in PEM format for generating RSA-PSS signatures.</param>
        /// <exception cref="ArgumentNullException">Thrown when apiKey or privateKeyPem is null or empty.</exception>
        /// <exception cref="CryptographicException">Thrown when the private key cannot be loaded.</exception>
        public KalshiAuthenticationHandler(string apiKey, string privateKeyPem)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey));
            if (string.IsNullOrWhiteSpace(privateKeyPem))
                throw new ArgumentNullException(nameof(privateKeyPem));

            _apiKey = apiKey;
            _privateKey = LoadPrivateKey(privateKeyPem);
        }

        /// <summary>
        /// Adds authentication headers to the RestRequest.
        /// </summary>
        /// <param name="request">The RestRequest to add authentication headers to.</param>
        /// <param name="method">The HTTP method (GET, POST, etc.).</param>
        /// <param name="path">The API path (e.g., "/exchange/status").</param>
        /// <param name="body">The request body, if any.</param>
        public void AddAuthenticationHeaders(RestRequest request, string method, string path, string? body = null)
        {
            // Generate timestamp (milliseconds since epoch)
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            // Build the signature string
            var signatureString = BuildSignatureString(method, path, timestamp, body);

            // Generate RSA-PSS signature
            var signature = GenerateSignature(signatureString);

            // Add headers
            request.AddHeader("KALSHI-ACCESS-KEY", _apiKey);
            request.AddHeader("KALSHI-ACCESS-SIGNATURE", signature);
            request.AddHeader("KALSHI-ACCESS-TIMESTAMP", timestamp);
        }

        /// <summary>
        /// Builds the signature string according to Kalshi API requirements.
        /// Format: {method}\n{path}\n{timestamp}\n{body}
        /// </summary>
        private string BuildSignatureString(string method, string path, string timestamp, string? body)
        {
            var sb = new StringBuilder();
            sb.AppendLine(method.ToUpperInvariant());
            sb.AppendLine(path);
            sb.AppendLine(timestamp);
            if (!string.IsNullOrEmpty(body))
            {
                sb.Append(body);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Generates an RSA-PSS signature for the given data.
        /// </summary>
        /// <param name="data">The data to sign.</param>
        /// <returns>Base64-encoded signature.</returns>
        private string GenerateSignature(string data)
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = _privateKey.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            return Convert.ToBase64String(signatureBytes);
        }

        /// <summary>
        /// Loads a private key from PEM format.
        /// </summary>
        /// <param name="privateKeyPem">The private key in PEM format.</param>
        /// <returns>An RSA instance containing the private key.</returns>
        private RSA LoadPrivateKey(string privateKeyPem)
        {
            try
            {
                var rsa = RSA.Create();

                // Detect the key format based on the PEM header
                bool isPkcs1 = privateKeyPem.Contains("-----BEGIN RSA PRIVATE KEY-----");

                // Remove PEM headers/footers and whitespace
                var keyData = privateKeyPem
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Replace(" ", "");

                var keyBytes = Convert.FromBase64String(keyData);

                // Use the appropriate import method based on the format
                if (isPkcs1)
                {
                    // PKCS#1 format (RSA PRIVATE KEY)
                    rsa.ImportRSAPrivateKey(keyBytes, out _);
                }
                else
                {
                    // PKCS#8 format (PRIVATE KEY)
                    rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                }

                return rsa;
            }
            catch (Exception ex)
            {
                throw new CryptographicException("Failed to load private key. Ensure it is in valid PEM format.", ex);
            }
        }

        /// <summary>
        /// Disposes of the RSA instance.
        /// </summary>
        public void Dispose()
        {
            _privateKey?.Dispose();
        }
    }
}

