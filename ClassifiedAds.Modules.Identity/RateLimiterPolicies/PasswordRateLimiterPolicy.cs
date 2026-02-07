using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.RateLimiterPolicies;

/// <summary>
/// Rate limiter policy for password-related endpoints.
/// Prevents abuse of password reset/change functionality.
/// Limits: 3 requests per 5 minutes per IP.
/// </summary>
public class PasswordRateLimiterPolicy : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = "300";
        return default;
    };

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = $"password:{ipAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey,
            partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(5),
            });
    }
}
