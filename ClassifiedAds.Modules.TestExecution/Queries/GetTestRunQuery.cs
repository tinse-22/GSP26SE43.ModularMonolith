using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Queries;

public class GetTestRunQuery : IQuery<TestRunModel>
{
    public Guid TestSuiteId { get; set; }

    public Guid RunId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetTestRunQueryHandler : IQueryHandler<GetTestRunQuery, TestRunModel>
{
    private readonly IRepository<TestRun, Guid> _runRepository;

    public GetTestRunQueryHandler(IRepository<TestRun, Guid> runRepository)
    {
        _runRepository = runRepository;
    }

    public async Task<TestRunModel> HandleAsync(GetTestRunQuery query, CancellationToken cancellationToken = default)
    {
        var run = await _runRepository.FirstOrDefaultAsync(
            _runRepository.GetQueryableSet()
                .Where(x => x.Id == query.RunId && x.TestSuiteId == query.TestSuiteId));

        if (run == null)
        {
            throw new NotFoundException($"Khong tim thay test run voi ma '{query.RunId}'.");
        }

        return TestRunModel.FromEntity(run);
    }
}
