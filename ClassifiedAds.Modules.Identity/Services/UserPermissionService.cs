using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.Services;

/// <summary>
/// Service for retrieving user permissions from the database.
/// Combines role-based permissions with direct user claims.
/// </summary>
public class UserPermissionService : IUserPermissionService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly ILogger<UserPermissionService> _logger;

    // Well-known permission claim types
    private static readonly string[] PermissionClaimTypes =
    {
        "Permission",
        "permission",
        ClaimTypes.Role,
    };

    public UserPermissionService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ILogger<UserPermissionService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogDebug("User {UserId} not found when loading permissions", userId);
            return Array.Empty<string>();
        }

        // 1. Get direct user claims of type "Permission"
        var userClaims = await _userManager.GetClaimsAsync(user);
        foreach (var claim in userClaims)
        {
            if (PermissionClaimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
            {
                permissions.Add(claim.Value);
            }
        }

        // 2. Get role-based permissions
        var roles = await _userManager.GetRolesAsync(user);
        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var roleClaims = await _roleManager.GetClaimsAsync(role);
                foreach (var claim in roleClaims)
                {
                    if (PermissionClaimTypes.Contains(claim.Type, StringComparer.OrdinalIgnoreCase))
                    {
                        permissions.Add(claim.Value);
                    }
                }

                // Also add the role name itself as a permission
                // This allows checking "HasPermission(Admin)" style checks
                permissions.Add($"Role:{roleName}");
            }
        }

        _logger.LogDebug(
            "Loaded {Count} permissions for user {UserId}: {Permissions}",
            permissions.Count,
            userId,
            string.Join(", ", permissions.Take(10)));

        return permissions.ToList();
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            _logger.LogDebug("User {UserId} not found when loading roles", userId);
            return Array.Empty<string>();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }
}
