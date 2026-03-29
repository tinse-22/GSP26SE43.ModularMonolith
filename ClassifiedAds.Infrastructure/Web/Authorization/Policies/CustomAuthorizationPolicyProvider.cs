using ClassifiedAds.Infrastructure.Web.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace ClassifiedAds.Infrastructure.Web.Authorization.Policies;

internal class CustomAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public CustomAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Permission:", StringComparison.InvariantCultureIgnoreCase))
        {
            var policyBuilder = new AuthorizationPolicyBuilder();

            policyBuilder.RequireAuthenticatedUser();

            // Extract the permission name from the policy name
            // e.g., "Permission:Users.Read" -> "Users.Read"
            var permissionName = policyName["Permission:".Length..];
            policyBuilder.AddRequirements(new PermissionRequirement(permissionName));

            var policy = policyBuilder.Build();

            return Task.FromResult(policy);
        }

        return base.GetPolicyAsync(policyName);
    }
}
