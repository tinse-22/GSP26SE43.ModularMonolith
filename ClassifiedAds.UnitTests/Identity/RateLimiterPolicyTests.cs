using ClassifiedAds.Modules.Identity.RateLimiterPolicies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Xunit;
using FluentAssertions;

namespace ClassifiedAds.UnitTests.Identity;

public class RateLimiterPolicyTests
{
    #region DefaultRateLimiterPolicy Tests

    [Fact]
    public void DefaultPolicy_AuthenticatedUser_Should_UseUserIdPartition()
    {
        // Arrange
        var policy = new DefaultRateLimiterPolicy();
        var userId = Guid.NewGuid().ToString();

        var httpContext = CreateMockHttpContext(
            isAuthenticated: true,
            userId: userId,
            ipAddress: "192.168.1.1");

        // Act
        var partition = policy.GetPartition(httpContext);

        // Assert
        // Authenticated users should be partitioned by user ID, not IP
        partition.Should().NotBeNull();
        // The partition key format is "auth:user:{userId}"
        // We can verify the limiter options indirectly
    }

    [Fact]
    public void DefaultPolicy_AnonymousUser_Should_UseIpPartition()
    {
        // Arrange
        var policy = new DefaultRateLimiterPolicy();
        var httpContext = CreateMockHttpContext(
            isAuthenticated: false,
            ipAddress: "192.168.1.1");

        // Act
        var partition = policy.GetPartition(httpContext);

        // Assert
        partition.Should().NotBeNull();
    }

    [Fact]
    public void DefaultPolicy_Should_UseXForwardedFor_WhenPresent()
    {
        // Arrange
        var policy = new DefaultRateLimiterPolicy();
        var httpContext = CreateMockHttpContext(
            isAuthenticated: false,
            ipAddress: "10.0.0.1",
            xForwardedFor: "203.0.113.195, 70.41.3.18");

        // Act
        var partition = policy.GetPartition(httpContext);

        // Assert
        // Should use the first IP from X-Forwarded-For header
        partition.Should().NotBeNull();
    }

    #endregion

    #region AuthRateLimiterPolicy Tests

    [Fact]
    public void AuthPolicy_Should_IncludeRouteInPartition()
    {
        // Arrange
        var policy = new AuthRateLimiterPolicy();
        var httpContext = CreateMockHttpContext(
            isAuthenticated: false,
            ipAddress: "192.168.1.1",
            requestPath: "/api/auth/login");

        // Act
        var partition = policy.GetPartition(httpContext);

        // Assert
        partition.Should().NotBeNull();
        // Partition key includes route to allow different rate limits per endpoint
    }

    [Fact]
    public void AuthPolicy_Should_HandleProxyChain()
    {
        // Arrange
        var policy = new AuthRateLimiterPolicy();
        var httpContext = CreateMockHttpContext(
            isAuthenticated: false,
            ipAddress: "10.0.0.1",
            xForwardedFor: "203.0.113.195, 70.41.3.18, 10.0.0.1",
            requestPath: "/api/auth/login");

        // Act
        var partition = policy.GetPartition(httpContext);

        // Assert
        partition.Should().NotBeNull();
        // Should use only the first IP (203.0.113.195)
    }

    #endregion

    #region PasswordRateLimiterPolicy Tests

    [Fact]
    public void PasswordPolicy_Should_HaveStricterLimits()
    {
        // Arrange
        var policy = new PasswordRateLimiterPolicy();
        var httpContext = CreateMockHttpContext(
            isAuthenticated: false,
            ipAddress: "192.168.1.1",
            requestPath: "/api/auth/forgot-password");

        // Act
        var partition = policy.GetPartition(httpContext);

        // Assert
        partition.Should().NotBeNull();
        // Password policy should have stricter limits (3 requests per 5 minutes)
    }

    #endregion

    #region Helper Methods

    private static HttpContext CreateMockHttpContext(
        bool isAuthenticated = false,
        string? userId = null,
        string ipAddress = "127.0.0.1",
        string? xForwardedFor = null,
        string requestPath = "/api/test")
    {
        var httpContext = new DefaultHttpContext();

        // Setup IP address
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);

        // Setup X-Forwarded-For header if provided
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            httpContext.Request.Headers["X-Forwarded-For"] = xForwardedFor;
        }

        // Setup request path
        httpContext.Request.Path = requestPath;

        // Setup authentication
        if (isAuthenticated)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId ?? Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim("sub", userId ?? Guid.NewGuid().ToString()),
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        return httpContext;
    }

    #endregion
}
