using Microsoft.Extensions.Caching.Memory;
using System;

namespace ClassifiedAds.Modules.Identity.Services;

/// <summary>
/// In-memory implementation of token blacklist using IMemoryCache.
/// 
/// How it works:
/// - When a user logs out or changes password, the access token's JTI is stored in cache
/// - On every authenticated request, OnTokenValidated checks if the JTI is blacklisted
/// - Cache entries auto-expire when the original token would have expired (no memory leak)
/// 
/// Trade-offs:
/// - ✅ Fast O(1) lookup on every request
/// - ✅ No database queries needed
/// - ✅ Auto-cleanup via cache expiration
/// - ⚠️ Blacklist is lost on app restart (tokens become valid again until they expire naturally)
/// - ⚠️ Not shared across multiple server instances (use Redis for distributed scenarios)
/// </summary>
public class InMemoryTokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _cache;
    private const string BlacklistKeyPrefix = "token_blacklist_";

    public InMemoryTokenBlacklistService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void BlacklistToken(string jti, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrEmpty(jti))
        {
            return;
        }

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            // Auto-remove from cache when the token would have expired anyway
            AbsoluteExpiration = expiresAt,
            // Low priority so it can be evicted under memory pressure (security trade-off)
            Priority = CacheItemPriority.High,
        };

        _cache.Set($"{BlacklistKeyPrefix}{jti}", true, cacheEntryOptions);
    }

    public bool IsTokenBlacklisted(string jti)
    {
        if (string.IsNullOrEmpty(jti))
        {
            return false;
        }

        return _cache.TryGetValue($"{BlacklistKeyPrefix}{jti}", out _);
    }
}
