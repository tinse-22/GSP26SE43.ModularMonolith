namespace ClassifiedAds.Modules.Identity.ConfigurationOptions;

/// <summary>
/// Configuration options for password validation algorithms.
/// </summary>
public class PasswordValidationOptions
{
    /// <summary>
    /// Enable weak password dictionary check. Default: true.
    /// </summary>
    public bool EnableDictionaryCheck { get; set; } = true;

    /// <summary>
    /// Enable Have I Been Pwned API check for leaked passwords. Default: true.
    /// Uses k-Anonymity model (only sends first 5 chars of SHA-1 hash).
    /// </summary>
    public bool EnableHibpCheck { get; set; } = true;

    /// <summary>
    /// HIBP API base URL. Default: https://api.pwnedpasswords.com
    /// </summary>
    public string HibpApiBaseUrl { get; set; } = "https://api.pwnedpasswords.com";

    /// <summary>
    /// Timeout for HIBP API calls in seconds. Default: 5.
    /// </summary>
    public int HibpTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Cache HIBP results for this duration in hours. Default: 24.
    /// </summary>
    public int HibpCacheHours { get; set; } = 24;

    /// <summary>
    /// Enable pattern detection (keyboard patterns, sequences, repeated chars). Default: true.
    /// </summary>
    public bool EnablePatternDetection { get; set; } = true;

    /// <summary>
    /// Enable entropy calculation check. Default: true.
    /// </summary>
    public bool EnableEntropyCheck { get; set; } = true;

    /// <summary>
    /// Minimum entropy in bits required for password. Default: 40.
    /// </summary>
    public double MinimumEntropyBits { get; set; } = 40;

    /// <summary>
    /// Enable check for user-related information in password. Default: true.
    /// </summary>
    public bool EnableUserInfoCheck { get; set; } = true;

    /// <summary>
    /// Number of previous passwords to check against. Default: 5.
    /// </summary>
    public int PasswordHistoryCount { get; set; } = 5;

    /// <summary>
    /// Minimum time between password changes in hours. Default: 24.
    /// Set to 0 to disable.
    /// </summary>
    public int MinimumPasswordAgeHours { get; set; } = 0;

    /// <summary>
    /// Maximum password age in days before requiring change. Default: 0 (disabled).
    /// </summary>
    public int MaximumPasswordAgeDays { get; set; } = 0;
}
