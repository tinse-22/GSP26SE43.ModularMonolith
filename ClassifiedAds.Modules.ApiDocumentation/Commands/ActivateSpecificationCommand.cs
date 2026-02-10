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

public class ActivateSpecificationCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid CurrentUserId { get; set; }

    public bool Activate { get; set; }
}

public class ActivateSpecificationCommandHandler : ICommandHandler<ActivateSpecificationCommand>
{
    private readonly Dispatcher _dispatcher;
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;

    public ActivateSpecificationCommandHandler(
        Dispatcher dispatcher,
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository)
    {
        _dispatcher = dispatcher;
        _projectRepository = projectRepository;
        _specRepository = specRepository;
    }

    public async Task HandleAsync(ActivateSpecificationCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Load project, verify exists and ownership
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

        // 2. Load specification, verify belongs to project
        var spec = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet().Where(s => s.Id == command.SpecId && s.ProjectId == command.ProjectId));

        if (spec == null)
        {
            throw new NotFoundException($"Không tìm thấy specification với mã '{command.SpecId}'.");
        }

        // 3. Execute within transaction
        await _specRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (command.Activate)
            {
                // Deactivate old spec if different
                if (project.ActiveSpecId.HasValue && project.ActiveSpecId != command.SpecId)
                {
                    var oldSpec = await _specRepository.FirstOrDefaultAsync(
                        _specRepository.GetQueryableSet().Where(s => s.Id == project.ActiveSpecId.Value));
                    if (oldSpec != null)
                    {
                        oldSpec.IsActive = false;
                    }
                }

                spec.IsActive = true;
                project.ActiveSpecId = spec.Id;
            }
            else
            {
                // Deactivate
                if (project.ActiveSpecId != command.SpecId)
                {
                    throw new ValidationException("Specification này không đang được kích hoạt.");
                }

                spec.IsActive = false;
                project.ActiveSpecId = null;
            }

            await _projectRepository.UpdateAsync(project, ct);
            await _specRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);

        // Dispatch domain event after transaction so audit/outbox handlers can record activate/deactivate
        await _dispatcher.DispatchAsync(new EntityUpdatedEvent<ApiSpecification>(spec, DateTime.UtcNow), cancellationToken);
    }
}
