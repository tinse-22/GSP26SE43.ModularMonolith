using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ClassifiedAds.IntegrationTests.Infrastructure;

/// <summary>
/// Custom authentication handler for integration tests.
/// Bypasses actual authentication and creates a test principal with configurable claims.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "TestScheme";
    public const string TestUserId = "test-user-id-12345";
    public const string TestUserName = "testuser@example.com";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if the request wants to skip authentication (for testing 401/403)
        if (Context.Request.Headers.ContainsKey("X-Skip-Auth"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Authentication skipped for testing"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId),
            new Claim(ClaimTypes.Name, TestUserName),
            new Claim(ClaimTypes.Email, TestUserName),
            new Claim("sub", TestUserId),
            // Add common permissions that may be required
            new Claim("scope", "ClassifiedAds.WebAPI"),
        };

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
