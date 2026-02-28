using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetTestCaseDetailQuery : IQuery<TestCaseModel>
{
    public Guid TestSuiteId { get; set; }
    public Guid TestCaseId { get; set; }
}

public class GetTestCaseDetailQueryHandler : IQueryHandler<GetTestCaseDetailQuery, TestCaseModel>
{
    private readonly IRepository<TestCase, Guid> _testCaseRepository;

    public GetTestCaseDetailQueryHandler(IRepository<TestCase, Guid> testCaseRepository)
    {
        _testCaseRepository = testCaseRepository;
    }

    public async Task<TestCaseModel> HandleAsync(
        GetTestCaseDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        var testCase = await _testCaseRepository.FirstOrDefaultAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.Id == query.TestCaseId && x.TestSuiteId == query.TestSuiteId)
                .Include(x => x.Request)
                .Include(x => x.Expectation)
                .Include(x => x.Variables)
                .Include(x => x.Dependencies));

        if (testCase == null)
            throw new NotFoundException($"Không tìm thấy test case với mã '{query.TestCaseId}' trong test suite '{query.TestSuiteId}'.");

        return TestCaseModel.FromEntity(testCase);
    }
}
