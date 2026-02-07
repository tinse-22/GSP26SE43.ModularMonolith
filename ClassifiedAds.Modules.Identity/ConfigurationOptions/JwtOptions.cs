namespace ClassifiedAds.Modules.Identity.ConfigurationOptions;

/// <summary>
/// JWT configuration options for token generation.
/// Secret key should be stored in User Secrets (dev) or Azure Key Vault (prod).
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Secret key for signing JWT tokens.
    /// MUST be at least 32 characters for HMAC-SHA256.
    /// </summary>
    public string SecretKey { get; set; }

    /// <summary>
    /// Token issuer (e.g., "https://classifiedads.com").
    /// </summary>
    public string Issuer { get; set; } = "ClassifiedAds";

    /// <summary>
    /// Token audience (e.g., "ClassifiedAds.WebAPI").
    /// </summary>
    public string Audience { get; set; } = "ClassifiedAds.WebAPI";

    /// <summary>
    /// Access token expiration in minutes. Default: 60 minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token expiration in days. Default: 7 days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
