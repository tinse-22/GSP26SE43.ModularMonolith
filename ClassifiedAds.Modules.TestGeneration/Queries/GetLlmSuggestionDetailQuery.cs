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

public class GetLlmSuggestionDetailQuery : IQuery<LlmSuggestionModel>
{
    public Guid TestSuiteId { get; set; }
    public Guid SuggestionId { get; set; }
    public Guid CurrentUserId { get; set; }
}

public class GetLlmSuggestionDetailQueryHandler : IQueryHandler<GetLlmSuggestionDetailQuery, LlmSuggestionModel>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;

    public GetLlmSuggestionDetailQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
    }

    public async Task<LlmSuggestionModel> HandleAsync(
        GetLlmSuggestionDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == query.TestSuiteId));

        if (suite == null)
            throw new NotFoundException($"Không tìm thấy test suite với mã '{query.TestSuiteId}'.");

        ValidationException.Requires(
            suite.CreatedById == query.CurrentUserId,
            "Bạn không phải chủ sở hữu của test suite này.");

        var suggestion = await _suggestionRepository.FirstOrDefaultAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.Id == query.SuggestionId && x.TestSuiteId == query.TestSuiteId));

        if (suggestion == null)
            throw new NotFoundException($"Không tìm thấy suggestion với mã '{query.SuggestionId}'.");

        return LlmSuggestionModel.FromEntity(suggestion);
    }
}
