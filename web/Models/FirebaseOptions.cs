namespace web_asp.Models;

public class FirebaseOptions
{
    /// <summary>
    /// Firebase project identifier used by the Admin SDK.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Optional path to a service account JSON file.
    /// </summary>
    public string? CredentialPath { get; set; }

    /// <summary>
    /// Optional raw or base64-encoded service account JSON.
    /// </summary>
    public string? CredentialJson { get; set; }

    /// <summary>
    /// Cookie domain to use in production (e.g., kalshisignals.com).
    /// Leave blank for localhost.
    /// </summary>
    public string? CookieDomain { get; set; }
}
