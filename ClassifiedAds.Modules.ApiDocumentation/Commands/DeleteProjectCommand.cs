using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

public class DeleteProjectCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class DeleteProjectCommandHandler : ICommandHandler<DeleteProjectCommand>
{
    private readonly Dispatcher _dispatcher;
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;

    public DeleteProjectCommandHandler(
        Dispatcher dispatcher,
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository)
    {
        _dispatcher = dispatcher;
        _projectRepository = projectRepository;
        _specRepository = specRepository;
    }

    public async Task HandleAsync(DeleteProjectCommand command, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.FirstOrDefaultAsync(
            _projectRepository.GetQueryableSet().Where(p => p.Id == command.ProjectId));

        if (project == null)
        {
            throw new NotFoundException($"Không tìm thấy project với mã '{command.ProjectId}'.");
        }

        if (project.OwnerId != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền xóa project này.");
        }

        // Deactivate all specs and clear active spec reference
        if (project.ActiveSpecId.HasValue)
        {
            project.ActiveSpecId = null;
        }

        var activeSpecs = await _specRepository.GetQueryableSet()
            .Where(s => s.ProjectId == command.ProjectId && s.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var spec in activeSpecs)
        {
            spec.IsActive = false;
        }

        project.Status = ProjectStatus.Archived;

        await _projectRepository.UpdateAsync(project, cancellationToken);
        await _projectRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        await _dispatcher.DispatchAsync(new EntityDeletedEvent<Project>(project, DateTime.UtcNow), cancellationToken);
    }
}
