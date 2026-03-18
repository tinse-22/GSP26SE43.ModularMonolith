using ClassifiedAds.Application;
using ClassifiedAds.Contracts.TestGeneration.Services;
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
    private readonly ITestExecutionReadGatewayService _gatewayService;

    public GetTestRunQueryHandler(
        IRepository<TestRun, Guid> runRepository,
        ITestExecutionReadGatewayService gatewayService)
    {
        _runRepository = runRepository;
        _gatewayService = gatewayService;
    }

    public async Task<TestRunModel> HandleAsync(GetTestRunQuery query, CancellationToken cancellationToken = default)
    {
        var suiteContext = await _gatewayService.GetSuiteAccessContextAsync(query.TestSuiteId, cancellationToken);
        if (suiteContext.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        var run = await _runRepository.FirstOrDefaultAsync(
            _runRepository.GetQueryableSet()
                .Where(x => x.Id == query.RunId && x.TestSuiteId == query.TestSuiteId));

        if (run == null)
        {
            throw new NotFoundException($"Không tìm thấy test run với mã '{query.RunId}'.");
        }

        return TestRunModel.FromEntity(run);
    }
}
