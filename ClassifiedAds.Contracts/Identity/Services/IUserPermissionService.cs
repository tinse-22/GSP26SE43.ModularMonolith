using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.Identity.Services;

/// <summary>
/// Service for retrieving user permissions from the database.
/// Used by CustomClaimsTransformation to enrich user claims.
/// </summary>
public interface IUserPermissionService
{
    /// <summary>
    /// Gets all permissions for a user based on their roles and direct claims.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of permission names.</returns>
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all roles for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of role names.</returns>
    Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
}
