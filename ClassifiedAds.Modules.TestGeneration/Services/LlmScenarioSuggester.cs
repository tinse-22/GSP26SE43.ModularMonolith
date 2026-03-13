using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.Services;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Suggests boundary/negative test scenarios using LLM via n8n webhook.
/// Pipeline: Check cache → Build prompts → Call n8n → Log interaction → Cache results → Parse response.
/// Follows the same pattern as <see cref="HappyPathTestCaseGenerator"/>.
/// </summary>
public class LlmScenarioSuggester : ILlmScenarioSuggester
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    /// <summary>SuggestionType for boundary/negative scenarios (maps to LlmSuggestionCache.SuggestionType).</summary>
    private const int SuggestionTypeBoundaryNegative = 1;

    /// <summary>Cache TTL: 24 hours.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly IObservationConfirmationPromptBuilder _promptBuilder;
    private readonly IN8nIntegrationService _n8nService;
    private readonly ILlmAssistantGatewayService _llmGatewayService;
    private readonly ILogger<LlmScenarioSuggester> _logger;

    public LlmScenarioSuggester(
        IObservationConfirmationPromptBuilder promptBuilder,
        IN8nIntegrationService n8nService,
        ILlmAssistantGatewayService llmGatewayService,
        ILogger<LlmScenarioSuggester> logger)
    {
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _n8nService = n8nService ?? throw new ArgumentNullException(nameof(n8nService));
        _llmGatewayService = llmGatewayService ?? throw new ArgumentNullException(nameof(llmGatewayService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LlmScenarioSuggestionResult> SuggestScenariosAsync(
        LlmScenarioSuggestionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting LLM scenario suggestion. TestSuiteId={TestSuiteId}, EndpointCount={EndpointCount}",
            context.TestSuiteId, context.OrderedEndpoints.Count);

        // Step 1: Check cache for each endpoint
        var cacheKey = BuildCacheKey(context);
        var cachedResult = await TryGetCachedResultAsync(context, cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            _logger.LogInformation(
                "Using cached LLM scenario suggestions. TestSuiteId={TestSuiteId}, ScenarioCount={Count}",
                context.TestSuiteId, cachedResult.Scenarios.Count);
            return cachedResult;
        }

        // Step 2: Build prompts using Observation-Confirmation pattern
        var metadataMap = context.EndpointMetadata.ToDictionary(e => e.EndpointId);
        var orderedMetadata = context.OrderedEndpoints
            .Where(oe => metadataMap.ContainsKey(oe.EndpointId))
            .Select(oe => metadataMap[oe.EndpointId])
            .ToList();

        var promptContexts = EndpointPromptContextMapper.Map(orderedMetadata, context.Suite);
        var prompts = _promptBuilder.BuildForSequence(promptContexts);

        // Step 3: Build n8n payload
        var payload = BuildN8nPayload(context, metadataMap, prompts);

        // Step 4: Call n8n webhook
        _logger.LogInformation(
            "Calling n8n webhook '{WebhookName}' for boundary/negative scenario suggestion. TestSuiteId={TestSuiteId}",
            N8nWebhookNames.GenerateBoundaryNegative, context.TestSuiteId);

        var stopwatch = Stopwatch.StartNew();
        var n8nResponse = await _n8nService.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
            N8nWebhookNames.GenerateBoundaryNegative, payload, cancellationToken);
        stopwatch.Stop();

        var latencyMs = (int)stopwatch.ElapsedMilliseconds;

        // Step 5: Log interaction for audit
        await SaveInteractionAsync(context, payload, n8nResponse, latencyMs, cancellationToken);

        // Step 6: Parse response into domain models
        var scenarios = ParseScenarios(n8nResponse);

        // Step 7: Cache results
        await CacheResultsAsync(context, cacheKey, scenarios, cancellationToken);

        _logger.LogInformation(
            "LLM scenario suggestion complete. TestSuiteId={TestSuiteId}, ScenarioCount={Count}, Model={Model}, TokensUsed={Tokens}, LatencyMs={LatencyMs}",
            context.TestSuiteId, scenarios.Count, n8nResponse?.Model, n8nResponse?.TokensUsed, latencyMs);

        return new LlmScenarioSuggestionResult
        {
            Scenarios = scenarios,
            LlmModel = n8nResponse?.Model,
            TokensUsed = n8nResponse?.TokensUsed,
            LatencyMs = latencyMs,
            FromCache = false,
        };
    }

    private async Task<LlmScenarioSuggestionResult> TryGetCachedResultAsync(
        LlmScenarioSuggestionContext context,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        // Check cache per endpoint — if ALL endpoints have cache hits, assemble from cache
        var allCachedScenarios = new List<LlmSuggestedScenario>();
        var endpointIds = context.OrderedEndpoints.Select(e => e.EndpointId).ToList();

        foreach (var endpointId in endpointIds)
        {
            var cached = await _llmGatewayService.GetCachedSuggestionsAsync(
                endpointId, SuggestionTypeBoundaryNegative, cacheKey, cancellationToken);

            if (!cached.HasCache)
            {
                return null; // Cache miss — must call LLM for fresh results
            }

            try
            {
                var scenarios = JsonSerializer.Deserialize<List<LlmSuggestedScenario>>(cached.SuggestionsJson, JsonOpts);
                if (scenarios != null)
                {
                    allCachedScenarios.AddRange(scenarios);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Failed to deserialize cached suggestions for EndpointId={EndpointId}. Treating as cache miss.",
                    endpointId);
                return null;
            }
        }

        return new LlmScenarioSuggestionResult
        {
            Scenarios = allCachedScenarios,
            FromCache = true,
        };
    }

    private N8nBoundaryNegativePayload BuildN8nPayload(
        LlmScenarioSuggestionContext context,
        Dictionary<Guid, ApiEndpointMetadataDto> metadataMap,
        IReadOnlyList<ObservationConfirmationPrompt> prompts)
    {
        var endpointPayloads = new List<N8nBoundaryEndpointPayload>();

        for (int i = 0; i < context.OrderedEndpoints.Count; i++)
        {
            var orderItem = context.OrderedEndpoints[i];
            metadataMap.TryGetValue(orderItem.EndpointId, out var metadata);

            context.Suite.EndpointBusinessContexts.TryGetValue(orderItem.EndpointId, out var businessContext);

            N8nPromptPayload promptPayload = null;
            if (i < prompts.Count && prompts[i] != null)
            {
                var prompt = prompts[i];
                promptPayload = new N8nPromptPayload
                {
                    SystemPrompt = prompt.SystemPrompt,
                    CombinedPrompt = prompt.CombinedPrompt,
                    ObservationPrompt = prompt.ObservationPrompt,
                    ConfirmationPromptTemplate = prompt.ConfirmationPromptTemplate,
                };
            }

            endpointPayloads.Add(new N8nBoundaryEndpointPayload
            {
                EndpointId = orderItem.EndpointId,
                HttpMethod = orderItem.HttpMethod,
                Path = orderItem.Path,
                OperationId = metadata?.OperationId,
                OrderIndex = orderItem.OrderIndex,
                BusinessContext = businessContext,
                Prompt = promptPayload,
                ParameterSchemaPayloads = metadata?.ParameterSchemaPayloads?.ToList() ?? new List<string>(),
                ResponseSchemaPayloads = metadata?.ResponseSchemaPayloads?.ToList() ?? new List<string>(),
                ParameterDetails = BuildParameterDetails(context, orderItem.EndpointId),
            });
        }

        return new N8nBoundaryNegativePayload
        {
            TestSuiteId = context.TestSuiteId,
            TestSuiteName = context.Suite.Name,
            GlobalBusinessRules = context.Suite.GlobalBusinessRules,
            Endpoints = endpointPayloads,
        };
    }

    private IReadOnlyList<LlmSuggestedScenario> ParseScenarios(N8nBoundaryNegativeResponse response)
    {
        if (response?.Scenarios == null || response.Scenarios.Count == 0)
        {
            return Array.Empty<LlmSuggestedScenario>();
        }

        return response.Scenarios.Select(s => new LlmSuggestedScenario
        {
            EndpointId = s.EndpointId,
            ScenarioName = s.ScenarioName,
            Description = s.Description,
            SuggestedTestType = ParseTestType(s.TestType),
            SuggestedBody = s.Request?.Body,
            SuggestedPathParams = s.Request?.PathParams,
            SuggestedQueryParams = s.Request?.QueryParams,
            SuggestedHeaders = s.Request?.Headers,
            ExpectedStatusCode = s.Expectation?.ExpectedStatus?.FirstOrDefault() ?? 400,
            ExpectedBehavior = s.Expectation?.BodyContains?.FirstOrDefault(),
            Priority = s.Priority,
            Tags = s.Tags ?? new List<string>(),
            Variables = s.Variables ?? new List<N8nTestCaseVariable>(),
        }).ToList();
    }

    private static TestType ParseTestType(string testType)
    {
        if (string.IsNullOrWhiteSpace(testType))
        {
            return TestType.Negative;
        }

        return testType.Trim().ToLowerInvariant() switch
        {
            "boundary" => TestType.Boundary,
            "negative" => TestType.Negative,
            _ => TestType.Negative,
        };
    }

    private async Task SaveInteractionAsync(
        LlmScenarioSuggestionContext context,
        N8nBoundaryNegativePayload payload,
        N8nBoundaryNegativeResponse response,
        int latencyMs,
        CancellationToken cancellationToken)
    {
        try
        {
            await _llmGatewayService.SaveInteractionAsync(new SaveLlmInteractionRequest
            {
                UserId = context.UserId,
                InteractionType = 0, // ScenarioSuggestion
                InputContext = JsonSerializer.Serialize(payload, JsonOpts),
                LlmResponse = JsonSerializer.Serialize(response, JsonOpts),
                ModelUsed = response?.Model,
                TokensUsed = response?.TokensUsed ?? 0,
                LatencyMs = latencyMs,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // Do not fail the main flow if audit logging fails
            _logger.LogWarning(ex, "Failed to save LLM interaction for audit. TestSuiteId={TestSuiteId}", context.TestSuiteId);
        }
    }

    private async Task CacheResultsAsync(
        LlmScenarioSuggestionContext context,
        string cacheKey,
        IReadOnlyList<LlmSuggestedScenario> scenarios,
        CancellationToken cancellationToken)
    {
        try
        {
            // Group scenarios by endpoint and cache individually
            var byEndpoint = scenarios.GroupBy(s => s.EndpointId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var endpointId in context.OrderedEndpoints.Select(e => e.EndpointId))
            {
                var endpointScenarios = byEndpoint.TryGetValue(endpointId, out var list) ? list : new List<LlmSuggestedScenario>();
                var json = JsonSerializer.Serialize(endpointScenarios, JsonOpts);

                await _llmGatewayService.CacheSuggestionsAsync(
                    endpointId, SuggestionTypeBoundaryNegative, cacheKey, json, CacheTtl, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache LLM suggestions. TestSuiteId={TestSuiteId}", context.TestSuiteId);
        }
    }

    private static List<N8nParameterDetail> BuildParameterDetails(
        LlmScenarioSuggestionContext context, Guid endpointId)
    {
        if (context.EndpointParameterDetails == null ||
            !context.EndpointParameterDetails.TryGetValue(endpointId, out var detail) ||
            detail.Parameters == null)
        {
            return new List<N8nParameterDetail>();
        }

        return detail.Parameters.Select(p => new N8nParameterDetail
        {
            Name = p.Name,
            Location = p.Location,
            DataType = p.DataType,
            Format = p.Format,
            IsRequired = p.IsRequired,
            DefaultValue = p.DefaultValue,
        }).ToList();
    }

    private static string BuildCacheKey(LlmScenarioSuggestionContext context)
    {
        // Include suite ID + specification ID + endpoint IDs to produce a unique cache key
        var sb = new StringBuilder();
        sb.Append(context.TestSuiteId).Append(':');
        sb.Append(context.SpecificationId).Append(':');
        foreach (var ep in context.OrderedEndpoints.OrderBy(e => e.EndpointId))
        {
            sb.Append(ep.EndpointId).Append(',');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..16]; // 8-byte hex prefix
    }
}
