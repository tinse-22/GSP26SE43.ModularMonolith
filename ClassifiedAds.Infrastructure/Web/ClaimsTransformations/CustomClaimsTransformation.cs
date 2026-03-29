using ClassifiedAds.Contracts.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Infrastructure.Web.ClaimsTransformations;

/// <summary>
/// Transforms authenticated claims by enriching them with permissions from the database.
/// Permissions are cached using HybridCache with a sliding expiration.
/// </summary>
public class CustomClaimsTransformation : IClaimsTransformation
{
    private readonly HybridCache _cache;
    private readonly IUserPermissionService _permissionService;
    private readonly ILogger<CustomClaimsTransformation> _logger;

    public CustomClaimsTransformation(
        HybridCache cache,
        ILogger<CustomClaimsTransformation> logger,
        IUserPermissionService permissionService = null)
    {
        _cache = cache;
        _logger = logger;
        _permissionService = permissionService;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identities.FirstOrDefault(x => x.IsAuthenticated);
        if (identity == null)
        {
            return principal;
        }

        // Skip if permission service is not available (e.g., in migrator context)
        if (_permissionService == null)
        {
            return principal;
        }

        var userClaim = principal.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);

        if (Guid.TryParse(userClaim?.Value, out var userId))
        {
            var issuedAt = principal.Claims.FirstOrDefault(x => x.Type == "iat")?.Value ?? "default";

            var cacheKey = $"permissions/{userId}/{issuedAt}";

            var permissions = await _cache.GetOrCreateAsync(cacheKey,
                async (cancellationToken) => await GetPermissionsAsync(userId, cancellationToken),
                tags: ["permissions", $"permissions/{userId}"]);

            var claims = new List<Claim>();
            claims.AddRange(permissions.Select(p => new Claim("Permission", p)));
            claims.AddRange(principal.Claims);

            var newIdentity = new ClaimsIdentity(claims, identity.AuthenticationType);
            return new ClaimsPrincipal(newIdentity);
        }

        return principal;
    }

    private async Task<List<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var permissions = await _permissionService.GetUserPermissionsAsync(userId, cancellationToken);
            _logger.LogDebug("Loaded {Count} permissions for user {UserId}", permissions.Count, userId);
            return permissions.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load permissions for user {UserId}", userId);
            return new List<string>();
        }
    }
}
