using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Events;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Commands;

public class ArchiveProjectCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }

    public bool Archive { get; set; }
}

public class ArchiveProjectCommandHandler : ICommandHandler<ArchiveProjectCommand>
{
    private readonly Dispatcher _dispatcher;
    private readonly IRepository<Project, Guid> _projectRepository;

    public ArchiveProjectCommandHandler(
        Dispatcher dispatcher,
        IRepository<Project, Guid> projectRepository)
    {
        _dispatcher = dispatcher;
        _projectRepository = projectRepository;
    }

    public async Task HandleAsync(ArchiveProjectCommand command, CancellationToken cancellationToken = default)
    {
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

        project.Status = command.Archive ? ProjectStatus.Archived : ProjectStatus.Active;

        await _projectRepository.UpdateAsync(project, cancellationToken);
        await _projectRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // Dispatch domain event so audit/outbox handlers can record archive/unarchive
        await _dispatcher.DispatchAsync(new EntityUpdatedEvent<Project>(project, DateTime.UtcNow), cancellationToken);
    }
}
