using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetApiTestOrderGateStatusQuery : IQuery<ApiTestOrderGateStatusModel>
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetApiTestOrderGateStatusQueryHandler : IQueryHandler<GetApiTestOrderGateStatusQuery, ApiTestOrderGateStatusModel>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IApiTestOrderGateService _apiTestOrderGateService;

    public GetApiTestOrderGateStatusQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IApiTestOrderGateService apiTestOrderGateService)
    {
        _suiteRepository = suiteRepository;
        _apiTestOrderGateService = apiTestOrderGateService;
    }

    public async Task<ApiTestOrderGateStatusModel> HandleAsync(GetApiTestOrderGateStatusQuery query, CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == query.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{query.TestSuiteId}'.");
        }

        if (suite.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        return await _apiTestOrderGateService.GetGateStatusAsync(query.TestSuiteId, cancellationToken);
    }
}
