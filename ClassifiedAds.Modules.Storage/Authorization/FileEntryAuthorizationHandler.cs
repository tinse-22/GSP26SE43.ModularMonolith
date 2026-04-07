using ClassifiedAds.Modules.Storage.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Storage.Authorization;

public class FileEntryAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, FileEntry>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OperationAuthorizationRequirement requirement, FileEntry resource)
    {
        if (resource == null || context.User?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var userIdValue = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Task.CompletedTask;
        }

        if (resource.OwnerId.HasValue && resource.OwnerId.Value == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
