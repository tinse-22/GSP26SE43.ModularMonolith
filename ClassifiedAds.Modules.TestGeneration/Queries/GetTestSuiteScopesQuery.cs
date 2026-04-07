using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetTestSuiteScopesQuery : IQuery<List<TestSuiteScopeModel>>
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetTestSuiteScopesQueryHandler : IQueryHandler<GetTestSuiteScopesQuery, List<TestSuiteScopeModel>>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;

    public GetTestSuiteScopesQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
    }

    public async Task<List<TestSuiteScopeModel>> HandleAsync(GetTestSuiteScopesQuery query, CancellationToken cancellationToken = default)
    {
        if (query.CurrentUserId == Guid.Empty)
        {
            throw new ValidationException("CurrentUserId la bat buoc.");
        }

        var suites = await _suiteRepository.ToListAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.ProjectId == query.ProjectId
                    && x.CreatedById == query.CurrentUserId
                    && x.Status != TestSuiteStatus.Archived)
                .OrderByDescending(x => x.CreatedDateTime));

        var suiteIds = suites.Select(s => s.Id).ToList();
        var testCaseCounts = await _testCaseRepository.GetQueryableSet()
            .Where(tc => suiteIds.Contains(tc.TestSuiteId))
            .GroupBy(tc => tc.TestSuiteId)
            .Select(g => new { TestSuiteId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TestSuiteId, x => x.Count, cancellationToken);

        return suites.Select(s => TestSuiteScopeModel.FromEntity(
            s, testCaseCounts.GetValueOrDefault(s.Id, 0))).ToList();
    }
}
