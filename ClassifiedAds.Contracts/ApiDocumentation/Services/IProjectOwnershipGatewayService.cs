using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.ApiDocumentation.Services;

public interface IProjectOwnershipGatewayService
{
    Task<bool> IsProjectOwnedByUserAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct = default);
}
