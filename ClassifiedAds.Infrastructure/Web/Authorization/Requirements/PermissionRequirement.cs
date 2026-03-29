using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassifiedAds.Infrastructure.Web.Authorization.Requirements;

/// <summary>
/// Authorization requirement that checks for a specific permission.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionName)
    {
        PermissionName = permissionName ?? throw new ArgumentNullException(nameof(permissionName));
    }

    /// <summary>
    /// The required permission name.
    /// </summary>
    public string PermissionName { get; }
}

/// <summary>
/// Handler for PermissionRequirement that checks user claims for the required permission.
/// Supports:
/// - Direct "Permission" claims added by CustomClaimsTransformation
/// - Role-based permissions (Role:RoleName format)
/// - Admin role bypass (admins have all permissions)
/// </summary>
public class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ILogger<PermissionRequirementHandler> _logger;

    // Claim types that represent permissions
    private static readonly string[] PermissionClaimTypes =
    {
        "Permission",
        "permission",
        ClaimTypes.Role,
    };

    // Roles that have all permissions (superuser)
    private static readonly string[] SuperuserRoles =
    {
        "Admin",
        "Administrator",
        "SuperAdmin",
    };

    public PermissionRequirementHandler(ILogger<PermissionRequirementHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var user = context.User;

        // 1. Check if user is authenticated
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("Authorization failed: user not authenticated");
            context.Fail(new AuthorizationFailureReason(this, "User is not authenticated"));
            return Task.CompletedTask;
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // 2. Check for superuser roles (bypass all permission checks)
        if (HasSuperuserRole(user))
        {
            _logger.LogDebug(
                "Authorization succeeded: user {UserId} has superuser role for permission {Permission}",
                userId,
                requirement.PermissionName);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 3. Check for exact permission match
        if (HasPermission(user, requirement.PermissionName))
        {
            _logger.LogDebug(
                "Authorization succeeded: user {UserId} has permission {Permission}",
                userId,
                requirement.PermissionName);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 4. Check for hierarchical permissions (e.g., "Users.Manage" implies "Users.Read")
        if (HasHierarchicalPermission(user, requirement.PermissionName))
        {
            _logger.LogDebug(
                "Authorization succeeded: user {UserId} has hierarchical permission for {Permission}",
                userId,
                requirement.PermissionName);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 5. Permission denied
        _logger.LogWarning(
            "Authorization failed: user {UserId} lacks permission {Permission}",
            userId,
            requirement.PermissionName);

        context.Fail(new AuthorizationFailureReason(
            this,
            $"User lacks required permission: {requirement.PermissionName}"));

        return Task.CompletedTask;
    }

    private static bool HasSuperuserRole(ClaimsPrincipal user)
    {
        foreach (var superuserRole in SuperuserRoles)
        {
            // Check "Permission" claim with Role: prefix
            if (user.HasClaim("Permission", $"Role:{superuserRole}"))
            {
                return true;
            }

            // Check standard role claim
            if (user.IsInRole(superuserRole))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        foreach (var claimType in PermissionClaimTypes)
        {
            if (user.HasClaim(claimType, permission))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHierarchicalPermission(ClaimsPrincipal user, string permission)
    {
        // Permission hierarchy: "Resource.Manage" implies "Resource.Read", "Resource.Write"
        // Permission hierarchy: "Resource.*" implies all "Resource.X" permissions

        // Check for wildcard permission
        var resourcePrefix = GetResourcePrefix(permission);
        if (!string.IsNullOrEmpty(resourcePrefix))
        {
            var wildcardPermission = $"{resourcePrefix}.*";
            if (HasPermission(user, wildcardPermission))
            {
                return true;
            }

            // Check for "Manage" which implies all operations
            var managePermission = $"{resourcePrefix}.Manage";
            if (HasPermission(user, managePermission))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetResourcePrefix(string permission)
    {
        var dotIndex = permission.LastIndexOf('.');
        if (dotIndex > 0)
        {
            return permission[..dotIndex];
        }

        return null;
    }
}
