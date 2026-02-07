namespace ClassifiedAds.Modules.Identity.RateLimiterPolicies;

/// <summary>
/// Rate limiter policy names for Identity module.
/// </summary>
public static class RateLimiterPolicyNames
{
    /// <summary>
    /// Default policy for general Identity endpoints.
    /// </summary>
    public const string DefaultPolicy = "Identity.DefaultPolicy";

    /// <summary>
    /// Strict policy for authentication endpoints (login, register).
    /// Lower limits to prevent brute-force attacks.
    /// </summary>
    public const string AuthPolicy = "Identity.AuthPolicy";

    /// <summary>
    /// Policy for password-related endpoints.
    /// </summary>
    public const string PasswordPolicy = "Identity.PasswordPolicy";
}
