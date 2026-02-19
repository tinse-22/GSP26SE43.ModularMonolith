using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Queries;

public class GetExecutionEnvironmentQuery : IQuery<ExecutionEnvironmentModel>
{
    public Guid ProjectId { get; set; }

    public Guid EnvironmentId { get; set; }
}

public class GetExecutionEnvironmentQueryHandler : IQueryHandler<GetExecutionEnvironmentQuery, ExecutionEnvironmentModel>
{
    private readonly IRepository<ExecutionEnvironment, Guid> _envRepository;
    private readonly IExecutionAuthConfigService _authConfigService;

    public GetExecutionEnvironmentQueryHandler(
        IRepository<ExecutionEnvironment, Guid> envRepository,
        IExecutionAuthConfigService authConfigService)
    {
        _envRepository = envRepository;
        _authConfigService = authConfigService;
    }

    public async Task<ExecutionEnvironmentModel> HandleAsync(GetExecutionEnvironmentQuery query, CancellationToken cancellationToken = default)
    {
        var env = await _envRepository.FirstOrDefaultAsync(
            _envRepository.GetQueryableSet()
                .Where(x => x.Id == query.EnvironmentId && x.ProjectId == query.ProjectId));

        if (env == null)
        {
            throw new NotFoundException($"Không tìm thấy execution environment với mã '{query.EnvironmentId}'.");
        }

        return ExecutionEnvironmentModel.FromEntity(env, _authConfigService);
    }
}
