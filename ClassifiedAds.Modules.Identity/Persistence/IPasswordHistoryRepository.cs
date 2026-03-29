using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Identity.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Identity.Persistence;

public interface IPasswordHistoryRepository : IRepository<PasswordHistory, Guid>
{
    /// <summary>
    /// Gets the most recent password history entries for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="count">Maximum number of entries to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of password history entries, ordered by most recent first.</returns>
    Task<IReadOnlyList<PasswordHistory>> GetRecentByUserIdAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new password to the user's history.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="passwordHash">The hashed password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddPasswordHistoryAsync(
        Guid userId,
        string passwordHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes old password history entries beyond the specified count.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="keepCount">Number of recent entries to keep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of entries removed.</returns>
    Task<int> CleanupOldEntriesAsync(
        Guid userId,
        int keepCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timestamp of the user's most recent password change.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The timestamp, or null if no history exists.</returns>
    Task<DateTimeOffset?> GetLastPasswordChangeDateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
