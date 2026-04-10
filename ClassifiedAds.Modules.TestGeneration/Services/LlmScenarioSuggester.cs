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
    private const int MinAdaptiveScenariosPerEndpoint = 2;
    private const int MaxAdaptiveScenariosPerEndpoint = 6;

    private const string DefaultSuggestionSystemPrompt =
        "You are a senior QA engineer specialising in REST API security and robustness testing. " +
        "Generate happy-path, boundary, and negative test scenarios for REST API endpoints and return ONLY raw JSON.";

    private const string SuggestionRulesBlock =
        "=== RULES ===\n" +
        "1. For each endpoint generate 2-4 scenarios: include at least one HappyPath when endpoint is executable, plus Boundary/Negative where applicable.\n" +
        "2. HappyPath: valid request payload and expected success status (2xx) with realistic data.\n" +
        "3. Boundary: values at the edge of valid range (e.g. empty string, max length, 0, -1, very large number).\n" +
        "4. Negative: invalid type, missing required field, wrong auth, forbidden access, not found.\n" +
        "4. Use realistic but clearly synthetic test data.\n" +
        "5. endpointId must be the EXACT UUID from input.\n" +
        "6. testType must be exactly \"HappyPath\", \"Boundary\", or \"Negative\".\n" +
        "7. priority: \"High\" for auth/security issues, \"Medium\" for validation, \"Low\" for edge cases.\n" +
        "8. request.body must be a JSON string (serialized) or null.\n" +
        "9. expectation.expectedStatus must be an array of integers e.g. [400] or [401] or [404].";

    private const string SuggestionResponseFormatBlock =
        "=== RESPONSE FORMAT ===\n" +
        "Return ONLY this JSON structure:\n" +
        "{\n" +
        "  \"scenarios\": [\n" +
        "    {\n" +
        "      \"endpointId\": \"<exact UUID>\",\n" +
        "      \"scenarioName\": \"<short name e.g. 'Missing required field: email'>\",\n" +
        "      \"description\": \"<one sentence>\",\n" +
        "      \"testType\": \"HappyPath|Boundary|Negative\",\n" +
        "      \"priority\": \"High|Medium|Low\",\n" +
        "      \"tags\": [\"boundary\"],\n" +
        "      \"request\": {\n" +
        "        \"httpMethod\": \"<GET|POST|PUT|DELETE|PATCH>\",\n" +
        "        \"url\": \"<path>\",\n" +
        "        \"headers\": null,\n" +
        "        \"pathParams\": null,\n" +
        "        \"queryParams\": null,\n" +
        "        \"bodyType\": \"None|JSON\",\n" +
        "        \"body\": \"<serialized JSON string or null>\",\n" +
        "        \"timeout\": 30000\n" +
        "      },\n" +
        "      \"expectation\": {\n" +
        "        \"expectedStatus\": [400],\n" +
        "        \"bodyContains\": null,\n" +
        "        \"bodyNotContains\": null\n" +
        "      },\n" +
        "      \"variables\": []\n" +
        "    }\n" +
        "  ],\n" +
        "  \"model\": \"<model name>\",\n" +
        "  \"tokensUsed\": 0\n" +
        "}";

    private static readonly JsonSerializerOptions JsonOpts = new ()
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
    private readonly ILlmSuggestionFeedbackContextService _feedbackContextService;
    private readonly ILogger<LlmScenarioSuggester> _logger;

    public LlmScenarioSuggester(
        IObservationConfirmationPromptBuilder promptBuilder,
        IN8nIntegrationService n8nService,
        ILlmAssistantGatewayService llmGatewayService,
        ILlmSuggestionFeedbackContextService feedbackContextService,
        ILogger<LlmScenarioSuggester> logger)
    {
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _n8nService = n8nService ?? throw new ArgumentNullException(nameof(n8nService));
        _llmGatewayService = llmGatewayService ?? throw new ArgumentNullException(nameof(llmGatewayService));
        _feedbackContextService = feedbackContextService ?? throw new ArgumentNullException(nameof(feedbackContextService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LlmScenarioSuggestionResult> SuggestScenariosAsync(
        LlmScenarioSuggestionContext context,
        CancellationToken cancellationToken = default)
    {
        var algorithmProfile = context?.AlgorithmProfile ?? new GenerationAlgorithmProfile();
        var orderedSequence = algorithmProfile.UseDependencyAwareOrdering
            ? ApplyDependencyAwareOrdering(context.OrderedEndpoints)
            : context.OrderedEndpoints.ToList();

        _logger.LogInformation(
            "Starting LLM scenario suggestion. TestSuiteId={TestSuiteId}, EndpointCount={EndpointCount}",
            context.TestSuiteId, orderedSequence.Count);

        var feedbackContext = algorithmProfile.UseFeedbackLoopContext
            ? await BuildFeedbackContextSafeAsync(context, orderedSequence, cancellationToken)
            : LlmSuggestionFeedbackContextResult.Empty;

        // Step 1: Check cache for each endpoint
        var cacheKey = BuildCacheKey(context, orderedSequence, feedbackContext.FeedbackFingerprint);
        _logger.LogInformation(
            "Prepared LLM suggestion cache input. TestSuiteId={TestSuiteId}, FeedbackFingerprint={FeedbackFingerprint}, CacheKey={CacheKey}, EndpointCount={EndpointCount}",
            context.TestSuiteId,
            feedbackContext.FeedbackFingerprint,
            cacheKey,
            orderedSequence.Count);
        var cachedResult = algorithmProfile.UseFeedbackLoopContext
            ? await TryGetCachedResultAsync(orderedSequence, cacheKey, cancellationToken)
            : null;
        if (cachedResult != null)
        {
            _logger.LogInformation(
                "Using cached LLM scenario suggestions. TestSuiteId={TestSuiteId}, ScenarioCount={Count}, FeedbackFingerprint={FeedbackFingerprint}, CacheKey={CacheKey}",
                context.TestSuiteId,
                cachedResult.Scenarios.Count,
                feedbackContext.FeedbackFingerprint,
                cacheKey);
            return cachedResult;
        }

        // Step 2: Build prompts using Observation-Confirmation pattern
        var metadataMap = context.EndpointMetadata.ToDictionary(e => e.EndpointId);
        var orderedMetadata = orderedSequence
            .Where(oe => metadataMap.ContainsKey(oe.EndpointId))
            .Select(oe => metadataMap[oe.EndpointId])
            .ToList();

        IReadOnlyList<ObservationConfirmationPrompt> prompts = Array.Empty<ObservationConfirmationPrompt>();
        if (algorithmProfile.UseObservationConfirmationPrompting)
        {
            var promptContexts = EndpointPromptContextMapper.Map(orderedMetadata, context.Suite);
            prompts = _promptBuilder.BuildForSequence(promptContexts);
        }

        // Step 3: Build n8n payload
        var payload = BuildN8nPayload(
            context,
            orderedSequence,
            metadataMap,
            prompts,
            feedbackContext.EndpointFeedbackContexts,
            algorithmProfile);

        // Step 4: Call n8n webhook
        _logger.LogInformation(
            "Calling n8n webhook '{WebhookName}' for boundary/negative scenario suggestion. TestSuiteId={TestSuiteId}, FeedbackFingerprint={FeedbackFingerprint}, CacheKey={CacheKey}",
            N8nWebhookNames.GenerateLlmSuggestions,
            context.TestSuiteId,
            feedbackContext.FeedbackFingerprint,
            cacheKey);

        var stopwatch = Stopwatch.StartNew();
        var n8nResponse = await _n8nService.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
            N8nWebhookNames.GenerateLlmSuggestions, payload, cancellationToken);
        stopwatch.Stop();

        var latencyMs = (int)stopwatch.ElapsedMilliseconds;

        // Step 5: Log interaction for audit
        await SaveInteractionAsync(context, payload, n8nResponse, latencyMs, cancellationToken);

        // Step 6: Parse response into domain models
        var scenarios = ParseScenarios(n8nResponse);
        scenarios = EnsureAdaptiveCoverage(scenarios, orderedSequence, metadataMap);

        // Step 7: Cache results
        if (algorithmProfile.UseFeedbackLoopContext)
        {
            await CacheResultsAsync(context, orderedSequence, cacheKey, scenarios, cancellationToken);
        }

        _logger.LogInformation(
            "LLM scenario suggestion complete. TestSuiteId={TestSuiteId}, ScenarioCount={Count}, Model={Model}, TokensUsed={Tokens}, LatencyMs={LatencyMs}, FeedbackFingerprint={FeedbackFingerprint}, CacheKey={CacheKey}",
            context.TestSuiteId,
            scenarios.Count,
            n8nResponse?.Model,
            n8nResponse?.TokensUsed,
            latencyMs,
            feedbackContext.FeedbackFingerprint,
            cacheKey);

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
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        // Check cache per endpoint — if ALL endpoints have cache hits, assemble from cache
        var allCachedScenarios = new List<LlmSuggestedScenario>();
        var endpointIds = orderedEndpoints.Select(e => e.EndpointId).ToList();

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
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        Dictionary<Guid, ApiEndpointMetadataDto> metadataMap,
        IReadOnlyList<ObservationConfirmationPrompt> prompts,
        IReadOnlyDictionary<Guid, string> endpointFeedbackContexts,
        GenerationAlgorithmProfile algorithmProfile)
    {
        var endpointPayloads = new List<N8nBoundaryEndpointPayload>();

        for (int i = 0; i < orderedEndpoints.Count; i++)
        {
            var orderItem = orderedEndpoints[i];
            metadataMap.TryGetValue(orderItem.EndpointId, out var metadata);

            context.Suite.EndpointBusinessContexts.TryGetValue(orderItem.EndpointId, out var businessContext);

            ObservationConfirmationPrompt prompt = null;
            if (i < prompts.Count)
            {
                prompt = prompts[i];
            }

            var promptPayload = BuildEndpointPromptPayload(
                orderItem,
                context.Suite,
                metadata,
                businessContext,
                prompt);

            endpointPayloads.Add(new N8nBoundaryEndpointPayload
            {
                EndpointId = orderItem.EndpointId,
                HttpMethod = orderItem.HttpMethod,
                Path = orderItem.Path,
                OperationId = metadata?.OperationId,
                OrderIndex = orderItem.OrderIndex,
                BusinessContext = businessContext,
                FeedbackContext = endpointFeedbackContexts.TryGetValue(orderItem.EndpointId, out var feedbackContext)
                    ? feedbackContext
                    : string.Empty,
                Prompt = promptPayload,
                ParameterSchemaPayloads = algorithmProfile.UseSchemaRelationshipAnalysis
                    ? metadata?.ParameterSchemaPayloads?.ToList() ?? new List<string>()
                    : new List<string>(),
                ResponseSchemaPayloads = algorithmProfile.UseSchemaRelationshipAnalysis
                    ? metadata?.ResponseSchemaPayloads?.ToList() ?? new List<string>()
                    : new List<string>(),
                ParameterDetails = algorithmProfile.UseSemanticTokenMatching
                    ? BuildParameterDetails(context, orderItem.EndpointId)
                    : new List<N8nParameterDetail>(),
            });
        }

        return new N8nBoundaryNegativePayload
        {
            TestSuiteId = context.TestSuiteId,
            TestSuiteName = context.Suite.Name,
            GlobalBusinessRules = context.Suite.GlobalBusinessRules,
            AlgorithmProfile = algorithmProfile,
            PromptConfig = BuildSuggestionPromptConfig(context, prompts, orderedEndpoints, metadataMap),
            Endpoints = endpointPayloads,
        };
    }

    private static N8nPromptPayload BuildEndpointPromptPayload(
        ApiOrderItemModel orderItem,
        TestSuite suite,
        ApiEndpointMetadataDto metadata,
        string businessContext,
        ObservationConfirmationPrompt prompt)
    {
        var combinedPrompt = string.IsNullOrWhiteSpace(prompt?.CombinedPrompt)
            ? BuildFallbackCombinedPrompt(orderItem, suite, metadata, businessContext)
            : prompt.CombinedPrompt;

        return new N8nPromptPayload
        {
            SystemPrompt = string.IsNullOrWhiteSpace(prompt?.SystemPrompt)
                ? DefaultSuggestionSystemPrompt
                : prompt.SystemPrompt,
            CombinedPrompt = combinedPrompt,
            ObservationPrompt = string.IsNullOrWhiteSpace(prompt?.ObservationPrompt)
                ? combinedPrompt
                : prompt.ObservationPrompt,
            ConfirmationPromptTemplate = string.IsNullOrWhiteSpace(prompt?.ConfirmationPromptTemplate)
                ? "Generate only boundary/negative scenarios based on provided API details and business rules; return JSON only."
                : prompt.ConfirmationPromptTemplate,
        };
    }

    private static string BuildFallbackCombinedPrompt(
        ApiOrderItemModel orderItem,
        TestSuite suite,
        ApiEndpointMetadataDto metadata,
        string businessContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Endpoint Context (Fallback)");
        sb.AppendLine($"Method: {orderItem?.HttpMethod ?? metadata?.HttpMethod ?? "GET"}");
        sb.AppendLine($"Path: {orderItem?.Path ?? metadata?.Path ?? "/"}");
        sb.AppendLine($"OperationId: {metadata?.OperationId ?? "N/A"}");
        sb.AppendLine();
        sb.AppendLine("Generate boundary and negative scenarios for this endpoint only.");

        if (!string.IsNullOrWhiteSpace(suite?.GlobalBusinessRules))
        {
            sb.AppendLine();
            sb.AppendLine("## Global Business Rules");
            sb.AppendLine(suite.GlobalBusinessRules);
        }

        if (!string.IsNullOrWhiteSpace(businessContext))
        {
            sb.AppendLine();
            sb.AppendLine("## Endpoint Business Context");
            sb.AppendLine(businessContext);
        }

        if (metadata?.ParameterSchemaPayloads?.Any() == true)
        {
            sb.AppendLine();
            sb.AppendLine("## Parameter Schemas");
            foreach (var schema in metadata.ParameterSchemaPayloads.Where(x => !string.IsNullOrWhiteSpace(x)).Take(3))
            {
                sb.AppendLine(schema);
            }
        }

        if (metadata?.ResponseSchemaPayloads?.Any() == true)
        {
            sb.AppendLine();
            sb.AppendLine("## Response Schemas");
            foreach (var schema in metadata.ResponseSchemaPayloads.Where(x => !string.IsNullOrWhiteSpace(x)).Take(3))
            {
                sb.AppendLine(schema);
            }
        }

        return sb.ToString();
    }

    private static N8nSuggestionPromptConfig BuildSuggestionPromptConfig(
        LlmScenarioSuggestionContext context,
        IReadOnlyList<ObservationConfirmationPrompt> prompts,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        var systemPrompt = prompts?
            .Select(x => x?.SystemPrompt)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return new N8nSuggestionPromptConfig
        {
            SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? DefaultSuggestionSystemPrompt
                : systemPrompt,
            TaskInstruction = BuildSuggestionTaskInstruction(context?.Suite, orderedEndpoints, metadataMap),
            Rules = SuggestionRulesBlock,
            ResponseFormat = SuggestionResponseFormatBlock,
        };
    }

    private static string BuildSuggestionTaskInstruction(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        var suiteName = string.IsNullOrWhiteSpace(suite?.Name) ? "N/A" : suite.Name;
        var sb = new StringBuilder();
        sb.Append($"Generate happy-path, boundary, and negative test scenarios for this ordered REST API sequence (suite: {suiteName}).");

        if (orderedEndpoints != null && orderedEndpoints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("=== COVERAGE TARGET GUIDE (adaptive, not hard cap) ===");
            foreach (var endpoint in orderedEndpoints.OrderBy(x => x.OrderIndex))
            {
                ApiEndpointMetadataDto metadata = null;
                metadataMap?.TryGetValue(endpoint.EndpointId, out metadata);
                var target = ComputeAdaptiveScenarioTarget(endpoint, metadata);
                var hasBoundarySurface = HasBoundarySurface(endpoint, metadata);
                var expectedTypes = hasBoundarySurface
                    ? "HappyPath, Boundary, Negative"
                    : "HappyPath, Negative";

                sb.AppendLine(
                    $"- [{endpoint.OrderIndex}] {endpoint.HttpMethod} {endpoint.Path}: target ~{target} scenarios, prioritize {expectedTypes}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(suite?.GlobalBusinessRules))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("=== GLOBAL BUSINESS RULES ===");
            sb.Append(suite.GlobalBusinessRules);
        }

        return sb.ToString();
    }

    private IReadOnlyList<LlmSuggestedScenario> ParseScenarios(N8nBoundaryNegativeResponse response)
    {
        if (response?.Scenarios == null || response.Scenarios.Count == 0)
        {
            return Array.Empty<LlmSuggestedScenario>();
        }

        return response.Scenarios.Select(s =>
        {
            var parsedType = ParseTestType(s.TestType);
            var defaultStatus = parsedType == TestType.HappyPath ? 200 : 400;

            return new LlmSuggestedScenario
            {
                EndpointId = s.EndpointId,
                ScenarioName = s.ScenarioName,
                Description = s.Description,
                SuggestedTestType = parsedType,
                SuggestedBody = s.Request?.Body,
                SuggestedPathParams = s.Request?.PathParams,
                SuggestedQueryParams = s.Request?.QueryParams,
                SuggestedHeaders = s.Request?.Headers,
                ExpectedStatusCode = s.Expectation?.ExpectedStatus?.FirstOrDefault() ?? defaultStatus,
                ExpectedBehavior = s.Expectation?.BodyContains?.FirstOrDefault(),
                Priority = s.Priority,
                Tags = s.Tags ?? new List<string>(),
                Variables = s.Variables ?? new List<N8nTestCaseVariable>(),
            };
        }).ToList();
    }

    private IReadOnlyList<LlmSuggestedScenario> EnsureAdaptiveCoverage(
        IReadOnlyList<LlmSuggestedScenario> rawScenarios,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        if (orderedEndpoints == null || orderedEndpoints.Count == 0)
        {
            return rawScenarios ?? Array.Empty<LlmSuggestedScenario>();
        }

        var scenarios = (rawScenarios ?? Array.Empty<LlmSuggestedScenario>()).ToList();
        var byEndpoint = scenarios
            .GroupBy(x => x.EndpointId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var added = 0;

        foreach (var endpoint in orderedEndpoints.OrderBy(x => x.OrderIndex))
        {
            ApiEndpointMetadataDto metadata = null;
            metadataMap?.TryGetValue(endpoint.EndpointId, out metadata);

            if (!byEndpoint.TryGetValue(endpoint.EndpointId, out var endpointScenarios))
            {
                endpointScenarios = new List<LlmSuggestedScenario>();
                byEndpoint[endpoint.EndpointId] = endpointScenarios;
            }

            var types = endpointScenarios.Select(x => x.SuggestedTestType).ToHashSet();
            var target = ComputeAdaptiveScenarioTarget(endpoint, metadata);

            if (!types.Contains(TestType.HappyPath))
            {
                endpointScenarios.Add(CreateFallbackScenario(endpoint, metadata, TestType.HappyPath, endpointScenarios.Count + 1));
                types.Add(TestType.HappyPath);
                added++;
            }

            if (!types.Contains(TestType.Negative))
            {
                endpointScenarios.Add(CreateFallbackScenario(endpoint, metadata, TestType.Negative, endpointScenarios.Count + 1));
                types.Add(TestType.Negative);
                added++;
            }

            if (HasBoundarySurface(endpoint, metadata) && !types.Contains(TestType.Boundary))
            {
                endpointScenarios.Add(CreateFallbackScenario(endpoint, metadata, TestType.Boundary, endpointScenarios.Count + 1));
                types.Add(TestType.Boundary);
                added++;
            }

            while (endpointScenarios.Count < target)
            {
                var nextType = endpointScenarios.Count % 2 == 0
                    ? TestType.Boundary
                    : TestType.Negative;

                if (nextType == TestType.Boundary && !HasBoundarySurface(endpoint, metadata))
                {
                    nextType = TestType.Negative;
                }

                endpointScenarios.Add(CreateFallbackScenario(endpoint, metadata, nextType, endpointScenarios.Count + 1));
                added++;
            }
        }

        if (added > 0)
        {
            _logger.LogInformation(
                "Adaptive coverage added {AddedCount} fallback scenario(s) across {EndpointCount} endpoint(s).",
                added,
                orderedEndpoints.Count);
        }

        return orderedEndpoints
            .OrderBy(x => x.OrderIndex)
            .SelectMany(x => byEndpoint.TryGetValue(x.EndpointId, out var list)
                ? list
                : Enumerable.Empty<LlmSuggestedScenario>())
            .ToList();
    }

    private static int ComputeAdaptiveScenarioTarget(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        var score = MinAdaptiveScenariosPerEndpoint;

        var method = (endpoint?.HttpMethod ?? metadata?.HttpMethod ?? string.Empty).Trim().ToUpperInvariant();
        if (method is "POST" or "PUT" or "PATCH")
        {
            score += 1;
        }

        if ((endpoint?.IsAuthRelated ?? false) || (metadata?.IsAuthRelated ?? false))
        {
            score += 1;
        }

        var parameterCount = metadata?.ParameterNames?.Count ?? 0;
        if (parameterCount >= 3)
        {
            score += 1;
        }

        var schemaSignal = (metadata?.ParameterSchemaRefs?.Count ?? 0) + (metadata?.ResponseSchemaRefs?.Count ?? 0);
        if (schemaSignal >= 2)
        {
            score += 1;
        }

        if ((endpoint?.DependsOnEndpointIds?.Count ?? 0) > 0)
        {
            score += 1;
        }

        return Math.Clamp(score, MinAdaptiveScenariosPerEndpoint, MaxAdaptiveScenariosPerEndpoint);
    }

    private static bool HasBoundarySurface(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        var hasParams = (metadata?.ParameterNames?.Count ?? 0) > 0;
        var hasSchema = (metadata?.ParameterSchemaRefs?.Count ?? 0) > 0 || (metadata?.ParameterSchemaPayloads?.Count ?? 0) > 0;
        var hasPathParams = ParsePathParameters(endpoint?.Path ?? metadata?.Path).Count > 0;
        return hasParams || hasSchema || hasPathParams;
    }

    private static IReadOnlyList<string> ParsePathParameters(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        var parameters = new List<string>();
        var startIndex = 0;

        while ((startIndex = path.IndexOf('{', startIndex)) >= 0)
        {
            var endIndex = path.IndexOf('}', startIndex);
            if (endIndex < 0)
            {
                break;
            }

            var name = path.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                parameters.Add(name);
            }

            startIndex = endIndex + 1;
        }

        return parameters;
    }

    private static LlmSuggestedScenario CreateFallbackScenario(
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata,
        TestType type,
        int index)
    {
        var method = endpoint?.HttpMethod ?? metadata?.HttpMethod ?? "GET";
        var path = endpoint?.Path ?? metadata?.Path ?? "/";

        var expectedStatus = type switch
        {
            TestType.HappyPath => method.Equals("POST", StringComparison.OrdinalIgnoreCase) ? 201 : 200,
            TestType.Boundary => 400,
            _ => ((endpoint?.IsAuthRelated ?? false) || (metadata?.IsAuthRelated ?? false)) ? 401 : 400,
        };

        var namePrefix = type switch
        {
            TestType.HappyPath => "Happy Path",
            TestType.Boundary => "Boundary Validation",
            _ => "Negative Validation",
        };

        var desc = type switch
        {
            TestType.HappyPath => "Valid request should succeed with expected successful status.",
            TestType.Boundary => "Boundary input values should be validated correctly.",
            _ => "Invalid or unauthorized request should be rejected.",
        };

        var tags = type switch
        {
            TestType.HappyPath => new List<string> { "happy-path", "coverage-gap-fill" },
            TestType.Boundary => new List<string> { "boundary", "coverage-gap-fill" },
            _ => new List<string> { "negative", "coverage-gap-fill" },
        };

        return new LlmSuggestedScenario
        {
            EndpointId = endpoint.EndpointId,
            ScenarioName = $"{namePrefix}: {method} {path} ({index})",
            Description = desc,
            SuggestedTestType = type,
            SuggestedBody = null,
            SuggestedPathParams = new Dictionary<string, string>(),
            SuggestedQueryParams = new Dictionary<string, string>(),
            SuggestedHeaders = new Dictionary<string, string>(),
            ExpectedStatusCode = expectedStatus,
            ExpectedBehavior = null,
            Priority = type == TestType.HappyPath ? "Medium" : "High",
            Tags = tags,
            Variables = new List<N8nTestCaseVariable>(),
        };
    }

    private static TestType ParseTestType(string testType)
    {
        if (string.IsNullOrWhiteSpace(testType))
        {
            return TestType.Negative;
        }

        return testType.Trim().ToLowerInvariant() switch
        {
            "happypath" or "happy_path" or "happy-path" or "happy path" => TestType.HappyPath,
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
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        string cacheKey,
        IReadOnlyList<LlmSuggestedScenario> scenarios,
        CancellationToken cancellationToken)
    {
        try
        {
            // Group scenarios by endpoint and cache individually
            var byEndpoint = scenarios.GroupBy(s => s.EndpointId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var endpointId in orderedEndpoints.Select(e => e.EndpointId))
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

    private async Task<LlmSuggestionFeedbackContextResult> BuildFeedbackContextSafeAsync(
        LlmScenarioSuggestionContext context,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        CancellationToken cancellationToken)
    {
        var endpointIds = orderedEndpoints
            .Select(x => x.EndpointId)
            .Distinct()
            .ToArray();

        if (endpointIds.Length == 0)
        {
            return LlmSuggestionFeedbackContextResult.Empty;
        }

        try
        {
            return await _feedbackContextService.BuildAsync(context.TestSuiteId, endpointIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to build feedback context. Falling back to empty feedback. TestSuiteId={TestSuiteId}",
                context.TestSuiteId);
            return LlmSuggestionFeedbackContextResult.Empty;
        }
    }

    private static string BuildCacheKey(
        LlmScenarioSuggestionContext context,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        string feedbackFingerprint)
    {
        var sb = new StringBuilder();
        sb.Append(context.TestSuiteId).Append(':');
        sb.Append(context.SpecificationId).Append(':');
        sb.Append(string.IsNullOrWhiteSpace(feedbackFingerprint)
            ? LlmSuggestionFeedbackContextResult.Empty.FeedbackFingerprint
            : feedbackFingerprint);
        sb.Append(':');
        foreach (var ep in orderedEndpoints)
        {
            sb.Append(ep.OrderIndex).Append('|');
            sb.Append(ep.EndpointId).Append(',');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..16]; // 8-byte hex prefix
    }

    private static IReadOnlyList<ApiOrderItemModel> ApplyDependencyAwareOrdering(
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints)
    {
        if (orderedEndpoints == null || orderedEndpoints.Count <= 1)
        {
            return orderedEndpoints ?? Array.Empty<ApiOrderItemModel>();
        }

        var endpointMap = orderedEndpoints.ToDictionary(x => x.EndpointId);
        var indegree = orderedEndpoints.ToDictionary(x => x.EndpointId, _ => 0);
        var adjacency = orderedEndpoints.ToDictionary(x => x.EndpointId, _ => new List<Guid>());

        foreach (var item in orderedEndpoints)
        {
            if (item.DependsOnEndpointIds == null)
            {
                continue;
            }

            foreach (var dependency in item.DependsOnEndpointIds)
            {
                if (!endpointMap.ContainsKey(dependency))
                {
                    continue;
                }

                adjacency[dependency].Add(item.EndpointId);
                indegree[item.EndpointId] += 1;
            }
        }

        var ready = new SortedSet<(int orderIndex, Guid endpointId)>(
            indegree
                .Where(x => x.Value == 0)
                .Select(x => (endpointMap[x.Key].OrderIndex, x.Key)));

        var result = new List<ApiOrderItemModel>(orderedEndpoints.Count);
        while (ready.Count > 0)
        {
            var current = ready.Min;
            ready.Remove(current);
            result.Add(endpointMap[current.endpointId]);

            foreach (var next in adjacency[current.endpointId])
            {
                indegree[next] -= 1;
                if (indegree[next] == 0)
                {
                    ready.Add((endpointMap[next].OrderIndex, next));
                }
            }
        }

        if (result.Count == orderedEndpoints.Count)
        {
            return result;
        }

        return orderedEndpoints.OrderBy(x => x.OrderIndex).ToList();
    }
}
