using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetLlmSuggestionsQuery : IQuery<List<LlmSuggestionModel>>
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public string FilterByReviewStatus { get; set; }
    public string FilterByTestType { get; set; }
    public Guid? FilterByEndpointId { get; set; }
}

public class GetLlmSuggestionsQueryHandler : IQueryHandler<GetLlmSuggestionsQuery, List<LlmSuggestionModel>>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly IRepository<LlmSuggestionFeedback, Guid> _feedbackRepository;

    public GetLlmSuggestionsQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        IRepository<LlmSuggestionFeedback, Guid> feedbackRepository)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
        _feedbackRepository = feedbackRepository;
    }

    public async Task<List<LlmSuggestionModel>> HandleAsync(
        GetLlmSuggestionsQuery query,
        CancellationToken cancellationToken = default)
    {
        // Verify suite exists + ownership
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == query.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{query.TestSuiteId}'.");
        }

        ValidationException.Requires(
            suite.CreatedById == query.CurrentUserId,
            "Bạn không phải chủ sở hữu của test suite này.");

        var queryable = _suggestionRepository.GetQueryableSet()
            .Where(x => x.TestSuiteId == query.TestSuiteId);

        // Filter by review status
        if (!string.IsNullOrWhiteSpace(query.FilterByReviewStatus)
            && Enum.TryParse<ReviewStatus>(query.FilterByReviewStatus, true, out var reviewStatus))
        {
            queryable = queryable.Where(x => x.ReviewStatus == reviewStatus);
        }
        else
        {
            // Default view focuses on active queue and reviewed items, not historical superseded rows.
            queryable = queryable.Where(x => x.ReviewStatus != ReviewStatus.Superseded);
        }

        // Filter by test type
        if (!string.IsNullOrWhiteSpace(query.FilterByTestType)
            && Enum.TryParse<TestType>(query.FilterByTestType, true, out var testType))
        {
            queryable = queryable.Where(x => x.TestType == testType);
        }

        // Filter by endpoint
        if (query.FilterByEndpointId.HasValue)
        {
            queryable = queryable.Where(x => x.EndpointId == query.FilterByEndpointId.Value);
        }

        queryable = queryable.OrderBy(x => x.DisplayOrder);

        var suggestions = await _suggestionRepository.ToListAsync(queryable);
        if (suggestions.Count == 0)
        {
            return new List<LlmSuggestionModel>();
        }

        var suggestionIds = suggestions.Select(x => x.Id).ToArray();
        var feedbackRows = await _feedbackRepository.ToListAsync(
            _feedbackRepository.GetQueryableSet()
                .Where(x =>
                    x.TestSuiteId == query.TestSuiteId &&
                    suggestionIds.Contains(x.SuggestionId)));

        var feedbackLookup = feedbackRows
            .GroupBy(x => x.SuggestionId)
            .ToDictionary(x => x.Key, x => (IReadOnlyCollection<LlmSuggestionFeedback>)x.ToList());

        return suggestions.Select(x => MapSuggestion(x, query.CurrentUserId, feedbackLookup)).ToList();
    }

    private static LlmSuggestionModel MapSuggestion(
        LlmSuggestion suggestion,
        Guid currentUserId,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<LlmSuggestionFeedback>> feedbackLookup)
    {
        var model = LlmSuggestionModel.FromEntity(suggestion);
        feedbackLookup.TryGetValue(suggestion.Id, out var feedbackRows);
        feedbackRows ??= Array.Empty<LlmSuggestionFeedback>();

        model.CurrentUserFeedback = feedbackRows
            .Where(x => x.UserId == currentUserId)
            .Select(LlmSuggestionFeedbackModel.FromEntity)
            .FirstOrDefault();
        model.FeedbackSummary = LlmSuggestionFeedbackSummaryModel.FromEntities(feedbackRows);

        return model;
    }
}
