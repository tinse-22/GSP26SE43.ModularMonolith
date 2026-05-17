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

    Task<LlmScenarioSuggestionResult> SuggestLocalDraftAsync(
        LlmScenarioSuggestionContext context,
        CancellationToken cancellationToken = default);

    Task<N8nBoundaryNegativePayload> BuildAsyncRefinementPayloadAsync(
        LlmScenarioSuggestionContext context,
        Guid refinementJobId,
        string callbackUrl,
        string callbackApiKey,
        CancellationToken cancellationToken = default);

    LlmScenarioSuggestionResult ParseRefinementResponse(
        LlmScenarioSuggestionContext context,
        N8nBoundaryNegativeResponse response);
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

    public GenerationAlgorithmProfile AlgorithmProfile { get; set; } = new GenerationAlgorithmProfile();

    /// <summary>
    /// If true, skip cache lookup and force a live n8n generation.
    /// </summary>
    public bool BypassCache { get; set; }

    /// <summary>
    /// SRS document linked to the test suite. Null when no SRS is associated.
    /// Content (ParsedMarkdown or RawContent) is sent to n8n so the LLM can align
    /// generated test scenarios with documented requirements.
    /// </summary>
    public SrsDocument SrsDocument { get; set; }

    /// <summary>
    /// Individual requirements extracted from the SRS document.
    /// Passed to n8n so the LLM can generate traceable test scenarios.
    /// </summary>
    public IReadOnlyList<SrsRequirement> SrsRequirements { get; set; } = Array.Empty<SrsRequirement>();
}

public class LlmScenarioSuggestionResult
{
    public IReadOnlyList<LlmSuggestedScenario> Scenarios { get; set; } = Array.Empty<LlmSuggestedScenario>();

    public string LlmModel { get; set; }

    public int? TokensUsed { get; set; }

    public int? LatencyMs { get; set; }

    public bool FromCache { get; set; }

    public bool UsedLocalFallback { get; set; }
}

public class LlmSuggestedScenario
{
    public Guid EndpointId { get; set; }

    public string ScenarioName { get; set; }

    public string Description { get; set; }

    public TestType SuggestedTestType { get; set; } = TestType.Negative;

    public string SuggestedHttpMethod { get; set; }

    public string SuggestedUrl { get; set; }

    public string SuggestedBody { get; set; }

    public string SuggestedBodyType { get; set; }

    public Dictionary<string, string> SuggestedPathParams { get; set; }

    public Dictionary<string, string> SuggestedQueryParams { get; set; }

    public Dictionary<string, string> SuggestedHeaders { get; set; }

    /// <summary>
    /// Primary expected status code (backward compatibility).
    /// Use <see cref="ExpectedStatusCodes"/> for full list.
    /// </summary>
    public int ExpectedStatusCode { get; set; } = 400;

    /// <summary>
    /// List of all acceptable status codes for this scenario.
    /// If null or empty, falls back to <see cref="ExpectedStatusCode"/>.
    /// </summary>
    public List<int> ExpectedStatusCodes { get; set; }

    public string ExpectedBehavior { get; set; }

    /// <summary>Full list of body-contains patterns from LLM (e.g. ["success", "id"]).</summary>
    public List<string> SuggestedBodyContains { get; set; }

    /// <summary>Full list of body-not-contains patterns from LLM.</summary>
    public List<string> SuggestedBodyNotContains { get; set; }

    /// <summary>JSONPath assertions from LLM (e.g. {"$.success": "true"}).</summary>
    public Dictionary<string, string> SuggestedJsonPathChecks { get; set; }

    /// <summary>Header assertions from LLM (e.g. {"Content-Type": "application/json"}).</summary>
    public Dictionary<string, string> SuggestedHeaderChecks { get; set; }

    public string Priority { get; set; }

    public List<string> Tags { get; set; } = new List<string>();

    public List<N8nTestCaseVariable> Variables { get; set; } = new List<N8nTestCaseVariable>();

    /// <summary>
    /// SRS requirement UUIDs this scenario covers, as reported by the LLM.
    /// </summary>
    public List<Guid> CoveredRequirementIds { get; set; } = new List<Guid>();

    public string ExpectationSource { get; set; }

    public string RequirementCode { get; set; }

    public Guid? PrimaryRequirementId { get; set; }

    /// <summary>
    /// Gets the effective list of expected status codes, preferring the full list if available.
    /// </summary>
    /// <returns></returns>
    public List<int> GetEffectiveExpectedStatusCodes()
    {
        if (ExpectedStatusCodes != null && ExpectedStatusCodes.Count > 0)
        {
            return ExpectedStatusCodes;
        }

        return new List<int> { ExpectedStatusCode };
    }
}
