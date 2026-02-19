using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetTestSuiteScopesQuery : IQuery<List<TestSuiteScopeModel>>
{
    public Guid ProjectId { get; set; }
}

public class GetTestSuiteScopesQueryHandler : IQueryHandler<GetTestSuiteScopesQuery, List<TestSuiteScopeModel>>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;

    public GetTestSuiteScopesQueryHandler(IRepository<TestSuite, Guid> suiteRepository)
    {
        _suiteRepository = suiteRepository;
    }

    public async Task<List<TestSuiteScopeModel>> HandleAsync(GetTestSuiteScopesQuery query, CancellationToken cancellationToken = default)
    {
        var suites = await _suiteRepository.ToListAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.ProjectId == query.ProjectId && x.Status != TestSuiteStatus.Archived)
                .OrderByDescending(x => x.CreatedDateTime));

        return suites.Select(TestSuiteScopeModel.FromEntity).ToList();
    }
}
