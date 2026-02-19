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

public class GetLatestApiTestOrderProposalQuery : IQuery<ApiTestOrderProposalModel>
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetLatestApiTestOrderProposalQueryHandler : IQueryHandler<GetLatestApiTestOrderProposalQuery, ApiTestOrderProposalModel>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IApiTestOrderService _apiTestOrderService;

    public GetLatestApiTestOrderProposalQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IApiTestOrderService apiTestOrderService)
    {
        _suiteRepository = suiteRepository;
        _proposalRepository = proposalRepository;
        _apiTestOrderService = apiTestOrderService;
    }

    public async Task<ApiTestOrderProposalModel> HandleAsync(GetLatestApiTestOrderProposalQuery query, CancellationToken cancellationToken = default)
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

        var latestProposal = await _proposalRepository.FirstOrDefaultAsync(
            _proposalRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == query.TestSuiteId)
                .OrderByDescending(x => x.ProposalNumber));

        if (latestProposal == null)
        {
            throw new NotFoundException($"Không tìm thấy proposal cho test suite '{query.TestSuiteId}'.");
        }

        return ApiTestOrderModelMapper.ToModel(latestProposal, _apiTestOrderService);
    }
}
