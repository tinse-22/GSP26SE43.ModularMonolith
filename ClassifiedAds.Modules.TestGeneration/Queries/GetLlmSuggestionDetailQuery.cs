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
    private readonly IRepository<LlmSuggestionFeedback, Guid> _feedbackRepository;

    public GetLlmSuggestionDetailQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        IRepository<LlmSuggestionFeedback, Guid> feedbackRepository)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
        _feedbackRepository = feedbackRepository;
    }

    public async Task<LlmSuggestionModel> HandleAsync(
        GetLlmSuggestionDetailQuery query,
        CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == query.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Khong tim thay test suite voi ma '{query.TestSuiteId}'.");
        }

        ValidationException.Requires(
            suite.CreatedById == query.CurrentUserId,
            "Ban khong phai chu so huu cua test suite nay.");

        var suggestion = await _suggestionRepository.FirstOrDefaultAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.Id == query.SuggestionId && x.TestSuiteId == query.TestSuiteId));

        if (suggestion == null)
        {
            throw new NotFoundException($"Khong tim thay suggestion voi ma '{query.SuggestionId}'.");
        }

        var feedbackRows = await _feedbackRepository.ToListAsync(
            _feedbackRepository.GetQueryableSet()
                .Where(x =>
                    x.TestSuiteId == query.TestSuiteId &&
                    x.SuggestionId == suggestion.Id));

        var model = LlmSuggestionModel.FromEntity(suggestion);
        model.CurrentUserFeedback = feedbackRows
            .Where(x => x.UserId == query.CurrentUserId)
            .Select(LlmSuggestionFeedbackModel.FromEntity)
            .FirstOrDefault();
        model.FeedbackSummary = LlmSuggestionFeedbackSummaryModel.FromEntities(feedbackRows);

        return model;
    }
}
