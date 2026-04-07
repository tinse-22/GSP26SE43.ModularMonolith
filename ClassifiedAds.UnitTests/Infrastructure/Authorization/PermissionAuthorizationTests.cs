using ClassifiedAds.Infrastructure.Web.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ClassifiedAds.UnitTests.Infrastructure.Authorization;

public class PermissionAuthorizationTests
{
    [Fact]
    public async Task AuthorizationPolicyProvider_ShouldPreservePermissionPrefix()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddAuthorizationPolicies(typeof(PermissionAuthorizationTests).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var policyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync("Permission:GetProjects");

        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle(x => x is PermissionRequirement);
        policy.Requirements.OfType<PermissionRequirement>().Single().PermissionName.Should().Be("Permission:GetProjects");
    }

    [Fact]
    public async Task PermissionRequirementHandler_ShouldSucceed_WhenPermissionClaimMatchesPrefixedPermission()
    {
        var requirement = new PermissionRequirement("Permission:GetProjects");
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("Permission", "Permission:GetProjects"),
        ], "Test"));

        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        var handler = new PermissionRequirementHandler(Mock.Of<ILogger<PermissionRequirementHandler>>());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }
}
