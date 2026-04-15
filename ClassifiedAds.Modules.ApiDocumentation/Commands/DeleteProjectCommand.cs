using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Contracts.TestGeneration.Services;
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
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly ITestSuiteProjectService _testSuiteProjectService;

    public DeleteProjectCommandHandler(
        Dispatcher dispatcher,
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        ITestSuiteProjectService testSuiteProjectService)
    {
        _dispatcher = dispatcher;
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _subscriptionLimitService = subscriptionLimitService;
        _testSuiteProjectService = testSuiteProjectService;
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

        // Release MaxProjects quota so user can create a new project after deletion
        await _subscriptionLimitService.ReleaseUsageAsync(new IncrementUsageRequest
        {
            UserId = command.CurrentUserId,
            LimitType = LimitType.MaxProjects,
            IncrementValue = 1,
        }, cancellationToken);

        // Cascade-archive all TestSuites belonging to this project (cross-module)
        await _testSuiteProjectService.ArchiveByProjectIdAsync(command.ProjectId, cancellationToken);
    }
}
