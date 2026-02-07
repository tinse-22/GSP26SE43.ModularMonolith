using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.RateLimiterPolicies;

/// <summary>
/// Default rate limiter policy for Identity module.
/// Anonymous users: 60 requests/minute (lower to prevent abuse)
/// Authenticated users: 200 requests/minute (higher for trusted users)
/// Partition key: UserId for auth users, IP address for anonymous
/// </summary>
public class DefaultRateLimiterPolicy : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        return default;
    };

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        // For authenticated users: partition by UserId (not IP) to avoid NAT issues
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? httpContext.User.FindFirst("sub")?.Value
                      ?? httpContext.User.Identity.Name;

            var partitionKey = $"auth:user:{userId}";
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1),
                });
        }

        // For anonymous users: partition by IP with lower limit
        // Use X-Forwarded-For header if behind proxy, fallback to RemoteIpAddress
        var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                     ?? httpContext.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";

        // Take only the first IP if multiple (in case of proxy chain)
        if (ipAddress.Contains(','))
        {
            ipAddress = ipAddress.Split(',')[0].Trim();
        }

        var anonPartitionKey = $"anon:ip:{ipAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(anonPartitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 60, // Lower limit for anonymous users
                Window = TimeSpan.FromMinutes(1),
            });
    }
}
