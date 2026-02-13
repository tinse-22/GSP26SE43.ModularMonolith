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

public class DeleteEndpointCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid EndpointId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class DeleteEndpointCommandHandler : ICommandHandler<DeleteEndpointCommand>
{
    private readonly Dispatcher _dispatcher;
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;

    public DeleteEndpointCommandHandler(
        Dispatcher dispatcher,
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository)
    {
        _dispatcher = dispatcher;
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
    }

    public async Task HandleAsync(DeleteEndpointCommand command, CancellationToken cancellationToken = default)
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

        // 2. Verify spec belongs to project
        var spec = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet().Where(s => s.Id == command.SpecId && s.ProjectId == command.ProjectId));

        if (spec == null)
        {
            throw new NotFoundException($"Không tìm thấy specification với mã '{command.SpecId}'.");
        }

        // 3. Load endpoint, verify belongs to spec
        var endpoint = await _endpointRepository.FirstOrDefaultAsync(
            _endpointRepository.GetQueryableSet().Where(e => e.Id == command.EndpointId && e.ApiSpecId == command.SpecId));

        if (endpoint == null)
        {
            throw new NotFoundException($"Không tìm thấy endpoint với mã '{command.EndpointId}'.");
        }

        // 4. Delete in transaction (DB cascade deletes children)
        await _endpointRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            _endpointRepository.Delete(endpoint);
            await _endpointRepository.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken: cancellationToken);

        await _dispatcher.DispatchAsync(new EntityDeletedEvent<ApiEndpoint>(endpoint, DateTime.UtcNow), cancellationToken);
    }
}
