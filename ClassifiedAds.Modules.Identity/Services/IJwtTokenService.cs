using ClassifiedAds.Modules.Identity.Entities;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.Services;

public interface IJwtTokenService
{
    Task<(string AccessToken, string RefreshToken, int ExpiresIn)> GenerateTokensAsync(User user, IList<string> roles);

    Task<ClaimsPrincipal> ValidateRefreshTokenAsync(string refreshToken);

    Task RevokeRefreshTokenAsync(Guid userId);
}
