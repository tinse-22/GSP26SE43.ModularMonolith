using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Queries;

public class GetExecutionEnvironmentsQuery : IQuery<List<ExecutionEnvironmentModel>>
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetExecutionEnvironmentsQueryHandler : IQueryHandler<GetExecutionEnvironmentsQuery, List<ExecutionEnvironmentModel>>
{
    private readonly IRepository<ExecutionEnvironment, Guid> _envRepository;
    private readonly IExecutionAuthConfigService _authConfigService;
    private readonly IProjectOwnershipGatewayService _projectOwnershipGatewayService;

    public GetExecutionEnvironmentsQueryHandler(
        IRepository<ExecutionEnvironment, Guid> envRepository,
        IExecutionAuthConfigService authConfigService,
        IProjectOwnershipGatewayService projectOwnershipGatewayService)
    {
        _envRepository = envRepository;
        _authConfigService = authConfigService;
        _projectOwnershipGatewayService = projectOwnershipGatewayService;
    }

    public async Task<List<ExecutionEnvironmentModel>> HandleAsync(GetExecutionEnvironmentsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.CurrentUserId == Guid.Empty)
        {
            throw new ValidationException("CurrentUserId la bat buoc.");
        }

        var isProjectOwner = await _projectOwnershipGatewayService.IsProjectOwnedByUserAsync(
            query.ProjectId,
            query.CurrentUserId,
            cancellationToken);

        if (!isProjectOwner)
        {
            throw new ValidationException("Ban khong co quyen thao tac project nay.");
        }

        var environments = await _envRepository.ToListAsync(
            _envRepository.GetQueryableSet()
                .Where(x => x.ProjectId == query.ProjectId)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name));

        return environments.Select(env => ExecutionEnvironmentModel.FromEntity(env, _authConfigService)).ToList();
    }
}
