using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Suggests boundary/negative test scenarios using LLM via n8n webhook.
/// Caches results per endpoint via ILlmAssistantGatewayService.
/// </summary>
public interface ILlmScenarioSuggester
{
    Task<LlmScenarioSuggestionResult> SuggestScenariosAsync(
        LlmScenarioSuggestionContext context,
        CancellationToken cancellationToken = default);
}

public class LlmScenarioSuggestionContext
{
    public Guid TestSuiteId { get; set; }

    public Guid UserId { get; set; }

    public TestSuite Suite { get; set; }

    public IReadOnlyList<ApiEndpointMetadataDto> EndpointMetadata { get; set; } = Array.Empty<ApiEndpointMetadataDto>();

    public IReadOnlyList<ApiOrderItemModel> OrderedEndpoints { get; set; } = Array.Empty<ApiOrderItemModel>();

    public Guid SpecificationId { get; set; }

    public IReadOnlyDictionary<Guid, EndpointParameterDetailDto> EndpointParameterDetails { get; set; } = new Dictionary<Guid, EndpointParameterDetailDto>();
}

public class LlmScenarioSuggestionResult
{
    public IReadOnlyList<LlmSuggestedScenario> Scenarios { get; set; } = Array.Empty<LlmSuggestedScenario>();

    public string LlmModel { get; set; }

    public int? TokensUsed { get; set; }

    public int? LatencyMs { get; set; }

    public bool FromCache { get; set; }
}

public class LlmSuggestedScenario
{
    public Guid EndpointId { get; set; }

    public string ScenarioName { get; set; }

    public string Description { get; set; }

    public TestType SuggestedTestType { get; set; } = TestType.Negative;

    public string SuggestedBody { get; set; }

    public Dictionary<string, string> SuggestedPathParams { get; set; }

    public Dictionary<string, string> SuggestedQueryParams { get; set; }

    public Dictionary<string, string> SuggestedHeaders { get; set; }

    public int ExpectedStatusCode { get; set; } = 400;

    public string ExpectedBehavior { get; set; }

    public string Priority { get; set; }

    public List<string> Tags { get; set; } = new();

    public List<N8nTestCaseVariable> Variables { get; set; } = new();
}
