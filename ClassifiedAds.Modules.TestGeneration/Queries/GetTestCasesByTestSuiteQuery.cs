using ClassifiedAds.Application;
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

public class GetTestCasesByTestSuiteQuery : IQuery<List<TestCaseModel>>
{
    public Guid TestSuiteId { get; set; }
}

public class GetTestCasesByTestSuiteQueryHandler : IQueryHandler<GetTestCasesByTestSuiteQuery, List<TestCaseModel>>
{
    private readonly IRepository<TestCase, Guid> _testCaseRepository;

    public GetTestCasesByTestSuiteQueryHandler(IRepository<TestCase, Guid> testCaseRepository)
    {
        _testCaseRepository = testCaseRepository;
    }

    public async Task<List<TestCaseModel>> HandleAsync(GetTestCasesByTestSuiteQuery query, CancellationToken cancellationToken = default)
    {
        var testCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == query.TestSuiteId)
                .Include(x => x.Request)
                .Include(x => x.Expectation)
                .OrderBy(x => x.OrderIndex));

        return testCases.Select(TestCaseModel.FromEntity).ToList();
    }
}
