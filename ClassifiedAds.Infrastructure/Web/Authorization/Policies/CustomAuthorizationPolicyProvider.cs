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

            // Permission constants and seeded claims both use the full "Permission:Foo" value.
            policyBuilder.AddRequirements(new PermissionRequirement(policyName));

            var policy = policyBuilder.Build();

            return Task.FromResult(policy);
        }

        return base.GetPolicyAsync(policyName);
    }
}
