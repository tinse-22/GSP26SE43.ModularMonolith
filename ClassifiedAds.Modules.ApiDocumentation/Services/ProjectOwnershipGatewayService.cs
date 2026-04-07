using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

public class ProjectOwnershipGatewayService : IProjectOwnershipGatewayService
{
    private readonly IRepository<Project, Guid> _projectRepository;

    public ProjectOwnershipGatewayService(IRepository<Project, Guid> projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public Task<bool> IsProjectOwnedByUserAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (projectId == Guid.Empty || userId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return IsOwnedByUserCoreAsync(projectId, userId, ct);
    }

    private async Task<bool> IsOwnedByUserCoreAsync(Guid projectId, Guid userId, CancellationToken ct)
    {
        var ownerId = await _projectRepository.GetQueryableSet()
            .Where(x => x.Id == projectId)
            .Select(x => (Guid?)x.OwnerId)
            .FirstOrDefaultAsync(ct);

        if (!ownerId.HasValue)
        {
            // Backward compatibility for legacy records created before project ownership became mandatory.
            return true;
        }

        return ownerId.Value == userId;
    }
}
