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

public class DeleteSpecificationCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class DeleteSpecificationCommandHandler : ICommandHandler<DeleteSpecificationCommand>
{
    private readonly Dispatcher _dispatcher;
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;

    public DeleteSpecificationCommandHandler(
        Dispatcher dispatcher,
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository)
    {
        _dispatcher = dispatcher;
        _projectRepository = projectRepository;
        _specRepository = specRepository;
    }

    public async Task HandleAsync(DeleteSpecificationCommand command, CancellationToken cancellationToken = default)
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

        // 2. Load specification
        var spec = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet().Where(s => s.Id == command.SpecId && s.ProjectId == command.ProjectId));

        if (spec == null)
        {
            throw new NotFoundException($"Không tìm thấy specification với mã '{command.SpecId}'.");
        }

        // 3. Delete within transaction (deactivate if active, then delete)
        await _specRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (project.ActiveSpecId == command.SpecId)
            {
                project.ActiveSpecId = null;
                await _projectRepository.UpdateAsync(project, ct);
            }

            _specRepository.Delete(spec);
            await _specRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);

        // Raise domain event after transaction
        await _dispatcher.DispatchAsync(new EntityDeletedEvent<ApiSpecification>(spec, DateTime.UtcNow), cancellationToken);
    }
}
