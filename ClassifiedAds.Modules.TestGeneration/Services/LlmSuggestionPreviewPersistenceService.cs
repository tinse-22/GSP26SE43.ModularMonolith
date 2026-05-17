using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public interface ILlmSuggestionPreviewPersistenceService
{
    Task<List<LlmSuggestion>> ReplacePendingSuggestionsAsync(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> approvedOrder,
        LlmScenarioSuggestionResult llmResult,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}

public class LlmSuggestionPreviewPersistenceService : ILlmSuggestionPreviewPersistenceService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;

    public LlmSuggestionPreviewPersistenceService(IRepository<LlmSuggestion, Guid> suggestionRepository)
    {
        _suggestionRepository = suggestionRepository;
    }

    public async Task<List<LlmSuggestion>> ReplacePendingSuggestionsAsync(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> approvedOrder,
        LlmScenarioSuggestionResult llmResult,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var existingSuggestions = await _suggestionRepository.ToListAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == suite.Id
                    && !x.AppliedTestCaseId.HasValue));

        var now = DateTimeOffset.UtcNow;

        foreach (var existing in existingSuggestions)
        {
            existing.ReviewStatus = ReviewStatus.Superseded;
            existing.ReviewedById = actorUserId;
            existing.ReviewedAt = now;
            existing.UpdatedDateTime = now;
            existing.RowVersion = Guid.NewGuid().ToByteArray();
            await _suggestionRepository.UpdateAsync(existing, cancellationToken);
        }

        var suggestions = new List<LlmSuggestion>();
        var orderItemMap = approvedOrder.ToDictionary(e => e.EndpointId);
        int displayOrder = 0;

        foreach (var scenario in llmResult.Scenarios)
        {
            orderItemMap.TryGetValue(scenario.EndpointId, out var orderItem);

            var suggestion = new LlmSuggestion
            {
                Id = Guid.NewGuid(),
                TestSuiteId = suite.Id,
                EndpointId = scenario.EndpointId,
                CacheKey = null,
                DisplayOrder = displayOrder++,
                SuggestionType = scenario.SuggestedTestType == TestType.HappyPath
                    ? LlmSuggestionType.HappyPath
                    : LlmSuggestionType.BoundaryNegative,
                TestType = scenario.SuggestedTestType,
                SuggestedName = LlmSuggestionMaterializer.SanitizeName(scenario.ScenarioName, orderItem),
                SuggestedDescription = scenario.Description,
                SuggestedRequest = JsonSerializer.Serialize(new N8nTestCaseRequest
                {
                    HttpMethod = string.IsNullOrWhiteSpace(scenario.SuggestedHttpMethod)
                        ? orderItem?.HttpMethod
                        : scenario.SuggestedHttpMethod,
                    Url = string.IsNullOrWhiteSpace(scenario.SuggestedUrl)
                        ? orderItem?.Path
                        : scenario.SuggestedUrl,
                    BodyType = scenario.SuggestedBodyType,
                    Body = scenario.SuggestedBody,
                    PathParams = scenario.SuggestedPathParams,
                    QueryParams = scenario.SuggestedQueryParams,
                    Headers = scenario.SuggestedHeaders,
                }, JsonOpts),
                SuggestedExpectation = JsonSerializer.Serialize(new N8nTestCaseExpectation
                {
                    ExpectedStatus = scenario.ExpectedStatusCodes ?? new List<int>(),
                    BodyContains = scenario.SuggestedBodyContains ?? new List<string>(),
                    BodyNotContains = scenario.SuggestedBodyNotContains ?? new List<string>(),
                    JsonPathChecks = scenario.SuggestedJsonPathChecks ?? new Dictionary<string, string>(),
                    HeaderChecks = scenario.SuggestedHeaderChecks ?? new Dictionary<string, string>(),
                    ExpectationSource = scenario.ExpectationSource,
                    RequirementCode = scenario.RequirementCode,
                    PrimaryRequirementId = scenario.PrimaryRequirementId,
                }, JsonOpts),
                SuggestedVariables = scenario.Variables?.Count > 0
                    ? JsonSerializer.Serialize(scenario.Variables, JsonOpts)
                    : null,
                SuggestedTags = JsonSerializer.Serialize(scenario.Tags ?? new List<string>(), JsonOpts),
                Priority = LlmSuggestionMaterializer.ParsePriority(scenario.Priority),
                ReviewStatus = ReviewStatus.Pending,
                LlmModel = llmResult.LlmModel,
                TokensUsed = llmResult.TokensUsed,
                SrsDocumentId = suite.SrsDocumentId,
                CoveredRequirementIds = scenario.CoveredRequirementIds?.Count > 0
                    ? JsonSerializer.Serialize(scenario.CoveredRequirementIds, JsonOpts)
                    : null,
                CreatedDateTime = now,
                RowVersion = Guid.NewGuid().ToByteArray(),
            };

            suggestions.Add(suggestion);
            await _suggestionRepository.AddAsync(suggestion, cancellationToken);
        }

        await _suggestionRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return suggestions;
    }
}
