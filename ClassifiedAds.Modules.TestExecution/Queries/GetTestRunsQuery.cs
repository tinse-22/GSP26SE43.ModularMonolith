using ClassifiedAds.Application;
using ClassifiedAds.Application.Common.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Queries;

public class GetTestRunsQuery : IQuery<Paged<TestRunModel>>
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public TestRunStatus? Status { get; set; }

    /// <summary>
    /// Include ephemeral runs in the result set. Defaults to false.
    /// </summary>
    public bool IncludeEphemeral { get; set; } = false;
}

public class GetTestRunsQueryHandler : IQueryHandler<GetTestRunsQuery, Paged<TestRunModel>>
{
    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly ITestExecutionReadGatewayService _gatewayService;
    private readonly IRepository<ExecutionEnvironment, Guid> _envRepository;

    public GetTestRunsQueryHandler(
        IRepository<TestRun, Guid> runRepository,
        ITestExecutionReadGatewayService gatewayService,
        IRepository<ExecutionEnvironment, Guid> envRepository)
    {
        _runRepository = runRepository;
        _gatewayService = gatewayService;
        _envRepository = envRepository;
    }

    public async Task<Paged<TestRunModel>> HandleAsync(GetTestRunsQuery query, CancellationToken cancellationToken = default)
    {
        var suiteContext = await _gatewayService.GetSuiteAccessContextAsync(query.TestSuiteId, cancellationToken);
        if (suiteContext.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        var baseQuery = _runRepository.GetQueryableSet()
            .Where(x => x.TestSuiteId == query.TestSuiteId);

        if (!query.IncludeEphemeral)
        {
            baseQuery = baseQuery.Where(x => !x.IsEphemeral);
        }

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Status == query.Status.Value);
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var runs = await _runRepository.ToListAsync(
            baseQuery
                .OrderByDescending(x => x.CreatedDateTime)
                .ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize));

        // Resolve environment names for display (suiteContext already loaded above)

        var envIds = runs.Select(r => r.EnvironmentId).Distinct().ToList();
        var envs = envIds.Count > 0
            ? await _envRepository.ToListAsync(_envRepository.GetQueryableSet().Where(e => envIds.Contains(e.Id)))
            : new System.Collections.Generic.List<ExecutionEnvironment>();
        var envMap = envs.ToDictionary(e => e.Id, e => e.Name);

        var items = runs.Select(r =>
        {
            var m = TestRunModel.FromEntity(r);
            m.TestSuiteName = suiteContext?.Name ?? query.TestSuiteId.ToString();
            m.EnvironmentName = envMap.TryGetValue(r.EnvironmentId, out var n) ? n : r.EnvironmentId.ToString();
            return m;
        }).ToList();

        return new Paged<TestRunModel>
        {
            Items = items,
            TotalItems = totalCount,
            Page = pageNumber,
            PageSize = pageSize,
        };
    }
}
