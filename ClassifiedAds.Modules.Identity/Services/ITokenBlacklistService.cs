using System;

namespace ClassifiedAds.Modules.Identity.Services;

/// <summary>
/// Service to manage blacklisted (revoked) JWT access tokens.
/// When a user logs out or changes password, the current access token's JTI is blacklisted
/// so it cannot be used again even if it hasn't expired yet.
/// </summary>
public interface ITokenBlacklistService
{
    /// <summary>
    /// Blacklist a token by its JTI (JWT ID) claim.
    /// The token will be rejected on all subsequent requests.
    /// </summary>
    /// <param name="jti">The JTI claim value from the JWT token</param>
    /// <param name="expiresAt">When the token naturally expires (used for cache cleanup)</param>
    void BlacklistToken(string jti, DateTimeOffset expiresAt);

    /// <summary>
    /// Check if a token has been blacklisted.
    /// </summary>
    /// <param name="jti">The JTI claim value from the JWT token</param>
    /// <returns>True if the token is blacklisted and should be rejected</returns>
    bool IsTokenBlacklisted(string jti);
}
