using ClassifiedAds.Modules.Identity.Entities;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.Services;

public interface IJwtTokenService
{
    Task<(string AccessToken, string RefreshToken, int ExpiresIn)> GenerateTokensAsync(User user, IList<string> roles);

    /// <summary>
    /// Validates refresh token and returns new tokens with rotation.
    /// Old refresh token is invalidated after successful validation.
    /// </summary>
    /// <param name="refreshToken">The current refresh token</param>
    /// <returns>New access token, new refresh token, expiration, and user principal</returns>
    Task<(string AccessToken, string RefreshToken, int ExpiresIn, ClaimsPrincipal Principal)?> ValidateAndRotateRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Validates refresh token without rotation (for checking only).
    /// </summary>
    Task<ClaimsPrincipal?> ValidateRefreshTokenAsync(string refreshToken);

    Task RevokeRefreshTokenAsync(Guid userId);

    /// <summary>
    /// Revoke all refresh tokens for a user (logout everywhere).
    /// </summary>
    Task RevokeAllRefreshTokensAsync(Guid userId);
}
