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
}

public class GetTestRunsQueryHandler : IQueryHandler<GetTestRunsQuery, Paged<TestRunModel>>
{
    private readonly IRepository<TestRun, Guid> _runRepository;
    private readonly ITestExecutionReadGatewayService _gatewayService;

    public GetTestRunsQueryHandler(
        IRepository<TestRun, Guid> runRepository,
        ITestExecutionReadGatewayService gatewayService)
    {
        _runRepository = runRepository;
        _gatewayService = gatewayService;
    }

    public async Task<Paged<TestRunModel>> HandleAsync(GetTestRunsQuery query, CancellationToken cancellationToken = default)
    {
        var suiteContext = await _gatewayService.GetSuiteAccessContextAsync(query.TestSuiteId, cancellationToken);
        if (suiteContext.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("Ban khong co quyen thao tac test suite nay.");
        }

        var baseQuery = _runRepository.GetQueryableSet()
            .Where(x => x.TestSuiteId == query.TestSuiteId);

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

        return new Paged<TestRunModel>
        {
            Items = runs.Select(TestRunModel.FromEntity).ToList(),
            TotalItems = totalCount,
            Page = pageNumber,
            PageSize = pageSize,
        };
    }
}
