using ClassifiedAds.Application;
using ClassifiedAds.Application.Common.DTOs;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
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

    public GetTestRunsQueryHandler(IRepository<TestRun, Guid> runRepository)
    {
        _runRepository = runRepository;
    }

    public async Task<Paged<TestRunModel>> HandleAsync(GetTestRunsQuery query, CancellationToken cancellationToken = default)
    {
        var baseQuery = _runRepository.GetQueryableSet()
            .Where(x => x.TestSuiteId == query.TestSuiteId);

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Status == query.Status.Value);
        }

        var totalItems = await _runRepository.ToListAsync(baseQuery.Select(x => x.Id));
        var totalCount = totalItems.Count;

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
