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

public class GetTestCasesByTestSuiteQuery : IQuery<List<TestCaseModel>>
{
    public Guid TestSuiteId { get; set; }
    public TestType? FilterByTestType { get; set; }
    public bool IncludeDisabled { get; set; }
}

public class GetTestCasesByTestSuiteQueryHandler : IQueryHandler<GetTestCasesByTestSuiteQuery, List<TestCaseModel>>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;

    public GetTestCasesByTestSuiteQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
    }

    public async Task<List<TestCaseModel>> HandleAsync(
        GetTestCasesByTestSuiteQuery query,
        CancellationToken cancellationToken = default)
    {
        // Verify suite exists
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == query.TestSuiteId));

        if (suite == null)
            throw new NotFoundException($"Không tìm thấy test suite với mã '{query.TestSuiteId}'.");

        var queryable = _testCaseRepository.GetQueryableSet()
            .Where(x => x.TestSuiteId == query.TestSuiteId);

        if (query.FilterByTestType.HasValue)
            queryable = queryable.Where(x => x.TestType == query.FilterByTestType.Value);

        if (!query.IncludeDisabled)
            queryable = queryable.Where(x => x.IsEnabled);

        queryable = queryable
            .Include(x => x.Request)
            .Include(x => x.Expectation)
            .Include(x => x.Variables)
            .Include(x => x.Dependencies)
            .OrderBy(x => x.OrderIndex);

        var testCases = await _testCaseRepository.ToListAsync(queryable);

        return testCases.Select(TestCaseModel.FromEntity).ToList();
    }
}
