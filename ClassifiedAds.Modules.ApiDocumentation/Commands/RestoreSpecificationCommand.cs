using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

public class RestoreSpecificationCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class RestoreSpecificationCommandHandler : ICommandHandler<RestoreSpecificationCommand>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;

    public RestoreSpecificationCommandHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
    }

    public async Task HandleAsync(RestoreSpecificationCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Load project, verify ownership
        var project = await _projectRepository.FirstOrDefaultAsync(
            _projectRepository.GetQueryableSet().Where(p => p.Id == command.ProjectId));

        if (project == null)
        {
            throw new NotFoundException($"Không tìm thấy project với mã '{command.ProjectId}'.");
        }

        if (project.OwnerId != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác project này.");
        }

        // 2. Load specification including soft-deleted
        var spec = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet()
                .Where(s => s.Id == command.SpecId && s.ProjectId == command.ProjectId && s.IsDeleted));

        if (spec == null)
        {
            throw new NotFoundException($"Không tìm thấy specification đã xóa với mã '{command.SpecId}'.");
        }

        // 3. Restore
        spec.IsDeleted = false;
        spec.DeletedAt = null;
        await _specRepository.UpdateAsync(spec, cancellationToken);
        await _specRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
