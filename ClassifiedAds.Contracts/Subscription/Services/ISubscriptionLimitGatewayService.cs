using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.Subscription.Services;

public interface ISubscriptionLimitGatewayService
{
    /// <summary>
    /// Checks whether a user's current usage allows performing an action
    /// that consumes the given limit type.
    /// </summary>
    /// <param name="userId">The user to check.</param>
    /// <param name="limitType">The type of limit to check.</param>
    /// <param name="proposedIncrement">How much the operation would add (e.g., 1 for a new project, file size in MB for storage).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<LimitCheckResultDTO> CheckLimitAsync(
        Guid userId,
        LimitType limitType,
        decimal proposedIncrement = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a usage increment after a successful operation.
    /// </summary>
    Task IncrementUsageAsync(
        IncrementUsageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically checks and consumes a limit in a single operation.
    /// Eliminates the race condition between CheckLimitAsync and IncrementUsageAsync.
    /// Uses Serializable isolation with retry on transient conflicts.
    /// </summary>
    /// <param name="userId">The user to check.</param>
    /// <param name="limitType">The type of limit to consume.</param>
    /// <param name="incrementValue">How much to consume (e.g., 1 for a new project).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating whether consumption was allowed and the new usage level.</returns>
    Task<LimitCheckResultDTO> TryConsumeLimitAsync(
        Guid userId,
        LimitType limitType,
        decimal incrementValue = 1,
        CancellationToken cancellationToken = default);
}
