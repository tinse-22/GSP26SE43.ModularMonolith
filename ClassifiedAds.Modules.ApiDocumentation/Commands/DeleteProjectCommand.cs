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

public class DeleteProjectCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class DeleteProjectCommandHandler : ICommandHandler<DeleteProjectCommand>
{
    private readonly Dispatcher _dispatcher;
    private readonly IRepository<Project, Guid> _projectRepository;

    public DeleteProjectCommandHandler(
        Dispatcher dispatcher,
        IRepository<Project, Guid> projectRepository)
    {
        _dispatcher = dispatcher;
        _projectRepository = projectRepository;
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

        project.Status = ProjectStatus.Archived;

        await _projectRepository.UpdateAsync(project, cancellationToken);
        await _projectRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        await _dispatcher.DispatchAsync(new EntityDeletedEvent<Project>(project, DateTime.UtcNow), cancellationToken);
    }
}
