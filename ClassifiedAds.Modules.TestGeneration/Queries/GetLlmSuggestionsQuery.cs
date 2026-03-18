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

    public GetLlmSuggestionsQueryHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
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
            throw new NotFoundException($"Không tìm thấy test suite với mã '{query.TestSuiteId}'.");

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

        return suggestions.Select(LlmSuggestionModel.FromEntity).ToList();
    }
}
