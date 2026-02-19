using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetTestSuiteScopeQuery : IQuery<TestSuiteScopeModel>
{
    public Guid ProjectId { get; set; }

    public Guid SuiteId { get; set; }
}

public class GetTestSuiteScopeQueryHandler : IQueryHandler<GetTestSuiteScopeQuery, TestSuiteScopeModel>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;

    public GetTestSuiteScopeQueryHandler(IRepository<TestSuite, Guid> suiteRepository)
    {
        _suiteRepository = suiteRepository;
    }

    public async Task<TestSuiteScopeModel> HandleAsync(GetTestSuiteScopeQuery query, CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == query.SuiteId && x.ProjectId == query.ProjectId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{query.SuiteId}'.");
        }

        return TestSuiteScopeModel.FromEntity(suite);
    }
}
