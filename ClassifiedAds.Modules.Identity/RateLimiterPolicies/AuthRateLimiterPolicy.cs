using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.RateLimiterPolicies;

/// <summary>
/// Strict rate limiter policy for authentication endpoints (login/register).
/// Prevents brute-force attacks.
/// Limits: 5 requests per minute per IP + route combination.
/// Uses sliding window for smoother limiting.
/// </summary>
public class AuthRateLimiterPolicy : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = "60";

        // Add informative header for debugging
        context.HttpContext.Response.Headers["X-RateLimit-Policy"] = "AuthPolicy";
        return default;
    };

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        // Get real client IP (handle proxies)
        var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                     ?? httpContext.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";

        // Take only the first IP if multiple (proxy chain)
        if (ipAddress.Contains(','))
        {
            ipAddress = ipAddress.Split(',')[0].Trim();
        }

        // Include route in partition key to allow different limits per endpoint
        var endpoint = httpContext.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var partitionKey = $"auth:ip:{ipAddress}:route:{endpoint}";

        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4, // Check every 15 seconds
            });
    }
}
