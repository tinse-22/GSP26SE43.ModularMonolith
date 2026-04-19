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
        "4. Use UNIQUE synthetic test data for every generation: emails MUST include a random 4-char suffix (e.g. \"testuser_a3x7@example.com\"). NEVER reuse generic emails like \"test@example.com\".\n" +
        "   AUTH FLOW RULES:\n" +
        "   - Registration HappyPath: use a unique email, add variable extraction rules to capture the email and password used (variableName: \"registeredEmail\", \"registeredPassword\", extractFrom: \"RequestBody\").\n" +
        "   - Login HappyPath: use \"{{registeredEmail}}\" and \"{{registeredPassword}}\" from the registration step so the chain works when no email confirmation is required.\n" +
        "   - If the execution environment provides {{testEmail}} and {{testPassword}}, those override for pre-confirmed accounts (users who need email confirmation can set these).\n" +
        "5. endpointId must be the EXACT UUID from input.\n" +
        "6. testType must be exactly \"HappyPath\", \"Boundary\", or \"Negative\".\n" +
        "7. priority: \"High\" for auth/security issues, \"Medium\" for validation, \"Low\" for edge cases.\n" +
        "8. Respect endpoint contract strictly: preserve real parameter names and locations (path/query/header/body).\n" +
        "9. If endpoint has required path params, request.pathParams MUST include non-empty values for every required token.\n" +
        "10. If endpoint has required query params, request.queryParams MUST include non-empty values for every required query param.\n" +
        "11. If endpoint requires request body, request.bodyType must be JSON and request.body must be a non-empty serialized JSON string.\n" +
        "12. expectation.expectedStatus must be an array of integers e.g. [400] or [401] or [404].";

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
        "        \"pathParams\": {},\n" +
        "        \"queryParams\": {},\n" +
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
            feedbackContext.EndpointFeedbackContexts);

        var endpointContracts = BuildEndpointContracts(context, orderedSequence, metadataMap);

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
        var scenarios = ParseScenarios(n8nResponse, endpointContracts);
        scenarios = EnsureAdaptiveCoverage(scenarios, orderedSequence, metadataMap, endpointContracts);

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
        IReadOnlyDictionary<Guid, string> endpointFeedbackContexts)
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
            AlgorithmProfile = context.AlgorithmProfile ?? new GenerationAlgorithmProfile(),
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

    private IReadOnlyList<LlmSuggestedScenario> ParseScenarios(
        N8nBoundaryNegativeResponse response,
        IReadOnlyDictionary<Guid, EndpointRequestContract> endpointContracts)
    {
        if (response?.Scenarios == null || response.Scenarios.Count == 0)
        {
            return Array.Empty<LlmSuggestedScenario>();
        }

        var scenarios = new List<LlmSuggestedScenario>(response.Scenarios.Count);

        foreach (var s in response.Scenarios)
        {
            var parsedType = ParseTestType(s.TestType);
            var expectedStatuses = BuildExpectedStatuses(parsedType, s.Expectation?.ExpectedStatus, s.Request?.HttpMethod);

            var parsedScenario = new LlmSuggestedScenario
            {
                EndpointId = s.EndpointId,
                ScenarioName = s.ScenarioName,
                Description = s.Description,
                SuggestedTestType = parsedType,
                SuggestedBodyType = s.Request?.BodyType,
                SuggestedBody = s.Request?.Body,
                SuggestedPathParams = s.Request?.PathParams,
                SuggestedQueryParams = s.Request?.QueryParams,
                SuggestedHeaders = s.Request?.Headers,
                ExpectedStatusCode = expectedStatuses.First(),
                ExpectedStatusCodes = expectedStatuses,
                ExpectedBehavior = s.Expectation?.BodyContains?.FirstOrDefault(),
                Priority = s.Priority,
                Tags = s.Tags ?? new List<string>(),
                Variables = s.Variables ?? new List<N8nTestCaseVariable>(),
            };

            if (endpointContracts != null &&
                endpointContracts.TryGetValue(parsedScenario.EndpointId, out var contract))
            {
                parsedScenario = ContractAwareRequestSynthesizer.RepairScenario(parsedScenario, contract.RequestContext);

                if (!IsScenarioContractComplete(parsedScenario, contract, out var missingParts))
                {
                    _logger.LogWarning(
                        "Discarding incomplete LLM scenario. EndpointId={EndpointId}, ScenarioName={ScenarioName}, Missing={Missing}",
                        parsedScenario.EndpointId,
                        parsedScenario.ScenarioName,
                        string.Join(", ", missingParts));
                    continue;
                }
            }

            scenarios.Add(parsedScenario);
        }

        return scenarios;
    }

    private static bool IsScenarioContractComplete(
        LlmSuggestedScenario scenario,
        EndpointRequestContract contract,
        out List<string> missingParts)
    {
        missingParts = new List<string>();

        foreach (var requiredPathParam in contract.RequiredPathParams)
        {
            if (scenario.SuggestedPathParams == null
                || !scenario.SuggestedPathParams.TryGetValue(requiredPathParam, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                missingParts.Add($"pathParams.{requiredPathParam}");
            }
        }

        foreach (var requiredQueryParam in contract.RequiredQueryParams)
        {
            if (scenario.SuggestedQueryParams == null
                || !scenario.SuggestedQueryParams.TryGetValue(requiredQueryParam, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                missingParts.Add($"queryParams.{requiredQueryParam}");
            }
        }

        if (contract.RequiresBody)
        {
            if (string.IsNullOrWhiteSpace(scenario.SuggestedBodyType)
                || scenario.SuggestedBodyType.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                missingParts.Add("request.bodyType");
            }

            if (string.IsNullOrWhiteSpace(scenario.SuggestedBody))
            {
                missingParts.Add("request.body");
            }
        }

        return missingParts.Count == 0;
    }

    private static List<int> BuildExpectedStatuses(TestType testType, List<int> source, string httpMethod)
    {
        var normalized = source?
            .Where(code => code >= 100 && code <= 599)
            .Distinct()
            .ToList() ?? new List<int>();

        if (testType == TestType.HappyPath)
        {
            var happyStatuses = normalized.Where(code => code >= 200 && code <= 299).ToList();
            return MergeStatuses(happyStatuses, GetHappyPathDefaultStatuses(httpMethod));
        }

        // Boundary/Negative should not accept success statuses.
        var nonSuccessStatuses = normalized.Where(code => code < 200 || code >= 300).ToList();
        return MergeStatuses(nonSuccessStatuses, GetBoundaryNegativeDefaultStatuses(httpMethod));
    }

    private static List<int> GetHappyPathDefaultStatuses(string httpMethod)
    {
        var method = NormalizeHttpMethod(httpMethod);
        return method switch
        {
            "POST" => new List<int> { 201, 200 },
            "PUT" => new List<int> { 200, 204 },
            "PATCH" => new List<int> { 200, 204 },
            "DELETE" => new List<int> { 204, 200, 202 },
            _ => new List<int> { 200 },
        };
    }

    private static List<int> GetBoundaryNegativeDefaultStatuses(string httpMethod)
    {
        _ = NormalizeHttpMethod(httpMethod);
        return new List<int> { 400, 401, 403, 404, 409, 415, 422 };
    }

    private static List<int> MergeStatuses(List<int> source, List<int> fallback)
    {
        var merged = new List<int>();

        if (source != null)
        {
            foreach (var code in source)
            {
                if (!merged.Contains(code))
                {
                    merged.Add(code);
                }
            }
        }

        if (fallback != null)
        {
            foreach (var code in fallback)
            {
                if (!merged.Contains(code))
                {
                    merged.Add(code);
                }
            }
        }

        if (merged.Count == 0)
        {
            merged.Add(200);
        }

        return merged;
    }

    private static string NormalizeHttpMethod(string httpMethod)
    {
        return string.IsNullOrWhiteSpace(httpMethod)
            ? "GET"
            : httpMethod.Trim().ToUpperInvariant();
    }

    private IReadOnlyList<LlmSuggestedScenario> EnsureAdaptiveCoverage(
        IReadOnlyList<LlmSuggestedScenario> rawScenarios,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap,
        IReadOnlyDictionary<Guid, EndpointRequestContract> endpointContracts)
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
                endpointContracts.TryGetValue(endpoint.EndpointId, out var contract);
                endpointScenarios.Add(CreateFallbackScenario(endpoint, metadata, contract, TestType.HappyPath, endpointScenarios.Count + 1));
                types.Add(TestType.HappyPath);
                added++;
            }

            if (!types.Contains(TestType.Negative))
            {
                endpointContracts.TryGetValue(endpoint.EndpointId, out var contract);
                endpointScenarios.Add(CreateFallbackScenario(endpoint, metadata, contract, TestType.Negative, endpointScenarios.Count + 1));
                types.Add(TestType.Negative);
                added++;
            }

            if (HasBoundarySurface(endpoint, metadata) && !types.Contains(TestType.Boundary))
            {
                endpointContracts.TryGetValue(endpoint.EndpointId, out var contract);
                endpointScenarios.Add(CreateFallbackScenario(endpoint, metadata, contract, TestType.Boundary, endpointScenarios.Count + 1));
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

                endpointContracts.TryGetValue(endpoint.EndpointId, out var contract);
                endpointScenarios.Add(CreateFallbackScenario(endpoint, metadata, contract, nextType, endpointScenarios.Count + 1));
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
        EndpointRequestContract contract,
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
        var expectedStatuses = BuildExpectedStatuses(type, new List<int> { expectedStatus }, method);

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

        var requestData = contract?.RequestContext != null
            ? ContractAwareRequestSynthesizer.BuildRequestData(contract.RequestContext, type)
            : new ContractAwareRequestData();

        return new LlmSuggestedScenario
        {
            EndpointId = endpoint.EndpointId,
            ScenarioName = $"{namePrefix}: {method} {path} ({index})",
            Description = desc,
            SuggestedTestType = type,
            SuggestedBodyType = requestData.BodyType,
            SuggestedBody = requestData.Body,
            SuggestedPathParams = requestData.PathParams,
            SuggestedQueryParams = requestData.QueryParams,
            SuggestedHeaders = requestData.Headers,
            ExpectedStatusCode = expectedStatuses.First(),
            ExpectedStatusCodes = expectedStatuses,
            ExpectedBehavior = null,
            Priority = type == TestType.HappyPath ? "Medium" : "High",
            Tags = tags,
            Variables = requestData.Variables,
        };
    }

    private static IReadOnlyDictionary<Guid, EndpointRequestContract> BuildEndpointContracts(
        LlmScenarioSuggestionContext context,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        var result = new Dictionary<Guid, EndpointRequestContract>();
        var orderItemMap = orderedEndpoints.ToDictionary(x => x.EndpointId);

        foreach (var endpoint in orderedEndpoints)
        {
            metadataMap.TryGetValue(endpoint.EndpointId, out var metadata);
            context.EndpointParameterDetails.TryGetValue(endpoint.EndpointId, out var parameterDetails);

            var requiredPathParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requiredQueryParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requiresBody = false;

            if (metadata?.RequiredPathParameterNames != null)
            {
                foreach (var pathParam in metadata.RequiredPathParameterNames)
                {
                    if (!string.IsNullOrWhiteSpace(pathParam))
                    {
                        requiredPathParams.Add(pathParam);
                    }
                }
            }

            if (metadata?.RequiredQueryParameterNames != null)
            {
                foreach (var queryParam in metadata.RequiredQueryParameterNames)
                {
                    if (!string.IsNullOrWhiteSpace(queryParam))
                    {
                        requiredQueryParams.Add(queryParam);
                    }
                }
            }

            if (metadata?.HasRequiredRequestBody == true)
            {
                requiresBody = true;
            }

            if (parameterDetails?.Parameters != null)
            {
                foreach (var parameter in parameterDetails.Parameters)
                {
                    if (!parameter.IsRequired || string.IsNullOrWhiteSpace(parameter.Name))
                    {
                        continue;
                    }

                    if (string.Equals(parameter.Location, "Path", StringComparison.OrdinalIgnoreCase))
                    {
                        requiredPathParams.Add(parameter.Name);
                    }
                    else if (string.Equals(parameter.Location, "Query", StringComparison.OrdinalIgnoreCase))
                    {
                        requiredQueryParams.Add(parameter.Name);
                    }
                    else if (string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase))
                    {
                        requiresBody = true;
                    }
                }
            }

            var requestContext = new ContractAwareRequestContext
            {
                HttpMethod = endpoint.HttpMethod ?? metadata?.HttpMethod,
                Path = endpoint.Path ?? metadata?.Path,
                OperationId = metadata?.OperationId,
                RequiresBody = requiresBody,
                RequiresAuth = RequiresAuth(endpoint, metadata, orderItemMap, metadataMap),
                IsRegisterLikeEndpoint = IsRegisterLikeEndpoint(endpoint, metadata),
                IsLoginLikeEndpoint = IsLoginLikeEndpoint(endpoint, metadata),
                RequiredPathParams = requiredPathParams.ToList(),
                RequiredQueryParams = requiredQueryParams.ToList(),
                Parameters = parameterDetails?.Parameters?.ToList() ?? new List<ParameterDetailDto>(),
                RequestBodySchema = ResolveRequestBodySchema(metadata, parameterDetails),
                RequestBodyExamples = ResolveRequestBodyExamples(metadata, parameterDetails),
                SuccessResponseSchema = ResolvePrimarySuccessResponseSchema(metadata),
                PlaceholderByFieldName = BuildPlaceholderMap(endpoint, metadata, orderItemMap, metadataMap),
            };

            result[endpoint.EndpointId] = new EndpointRequestContract(
                requiredPathParams,
                requiredQueryParams,
                requiresBody,
                requestContext);
        }

        return result;
    }

    private static string ResolveRequestBodySchema(
        ApiEndpointMetadataDto metadata,
        EndpointParameterDetailDto parameterDetails)
    {
        var bodyParameter = metadata?.Parameters?.FirstOrDefault(parameter =>
            string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(parameter.Schema));

        if (!string.IsNullOrWhiteSpace(bodyParameter?.Schema))
        {
            return bodyParameter.Schema;
        }

        var bodySchemaPayload = metadata?.ParameterSchemaPayloads?
            .FirstOrDefault(LooksLikeWholeRequestBodySchema);
        if (!string.IsNullOrWhiteSpace(bodySchemaPayload))
        {
            return bodySchemaPayload;
        }

        return parameterDetails?.Parameters?
            .Where(parameter =>
                string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(parameter.Schema))
            .Select(parameter => parameter.Schema)
            .FirstOrDefault(LooksLikeWholeRequestBodySchema);
    }

    private static string ResolveRequestBodyExamples(
        ApiEndpointMetadataDto metadata,
        EndpointParameterDetailDto parameterDetails)
    {
        var metadataExamples = metadata?.Parameters?
            .FirstOrDefault(parameter =>
                string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(parameter.Examples))
            ?.Examples;
        if (!string.IsNullOrWhiteSpace(metadataExamples))
        {
            return metadataExamples;
        }

        return parameterDetails?.Parameters?
            .Where(parameter =>
                string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(parameter.Examples))
            .Select(parameter => parameter.Examples)
            .FirstOrDefault(LooksLikeStructuredRequestBodyExample);
    }

    private static bool LooksLikeWholeRequestBodySchema(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(schema);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String)
            {
                var type = typeElement.GetString();
                if (string.Equals(type, "object", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type, "array", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return root.TryGetProperty("properties", out _)
                || root.TryGetProperty("items", out _)
                || root.TryGetProperty("allOf", out _)
                || root.TryGetProperty("oneOf", out _)
                || root.TryGetProperty("anyOf", out _);
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeStructuredRequestBodyExample(string example)
    {
        if (string.IsNullOrWhiteSpace(example))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(example);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolvePrimarySuccessResponseSchema(ApiEndpointMetadataDto metadata)
    {
        return metadata?.Responses?
            .Where(response => response is { StatusCode: >= 200 and < 300 } && !string.IsNullOrWhiteSpace(response.Schema))
            .OrderBy(response => response.StatusCode)
            .Select(response => response.Schema)
            .FirstOrDefault()
            ?? metadata?.ResponseSchemaPayloads?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static Dictionary<string, string> BuildPlaceholderMap(
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemMap,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var endpointPath = endpoint?.Path ?? metadata?.Path;
        var currentResourcePlaceholder = BuildPreferredVariableName("id", endpointPath);
        if (!string.IsNullOrWhiteSpace(currentResourcePlaceholder))
        {
            placeholders["id"] = currentResourcePlaceholder;
        }

        var dependencyIds = endpoint?.DependsOnEndpointIds?.AsEnumerable() ?? Array.Empty<Guid>();
        foreach (var dependencyId in dependencyIds)
        {
            orderItemMap.TryGetValue(dependencyId, out var dependencyOrderItem);
            metadataMap.TryGetValue(dependencyId, out var dependencyMetadata);

            var dependencyPath = dependencyOrderItem?.Path ?? dependencyMetadata?.Path;
            var dependencyPlaceholder = BuildPreferredVariableName("id", dependencyPath);
            if (!string.IsNullOrWhiteSpace(dependencyPlaceholder))
            {
                placeholders[dependencyPlaceholder] = dependencyPlaceholder;

                if (string.Equals(BuildResourceToken(endpointPath), BuildResourceToken(dependencyPath), StringComparison.OrdinalIgnoreCase))
                {
                    placeholders["id"] = dependencyPlaceholder;
                }
            }

            if (IsAuthLikeEndpoint(dependencyOrderItem, dependencyMetadata) &&
                IsLoginLikeEndpoint(endpoint, metadata))
            {
                placeholders["email"] = "registeredEmail";
                placeholders["password"] = "registeredPassword";
            }
        }

        return placeholders;
    }

    private static bool RequiresAuth(
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemMap,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        if (IsAuthLikeEndpoint(endpoint, metadata))
        {
            return false;
        }

        var dependencyIds = endpoint?.DependsOnEndpointIds?.AsEnumerable() ?? Array.Empty<Guid>();
        foreach (var dependencyId in dependencyIds)
        {
            orderItemMap.TryGetValue(dependencyId, out var dependencyOrderItem);
            metadataMap.TryGetValue(dependencyId, out var dependencyMetadata);
            if (IsAuthLikeEndpoint(dependencyOrderItem, dependencyMetadata))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRegisterLikeEndpoint(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        var signature = BuildEndpointSignature(endpoint, metadata);
        return signature.Contains("register", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("signup", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("sign-up", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoginLikeEndpoint(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        var signature = BuildEndpointSignature(endpoint, metadata);
        return signature.Contains("login", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("signin", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("sign-in", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("authenticate", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("/token", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthLikeEndpoint(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        return metadata?.IsAuthRelated == true
            || endpoint?.IsAuthRelated == true
            || IsRegisterLikeEndpoint(endpoint, metadata)
            || IsLoginLikeEndpoint(endpoint, metadata);
    }

    private static string BuildEndpointSignature(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        return string.Join(" ",
            endpoint?.HttpMethod,
            endpoint?.Path,
            metadata?.HttpMethod,
            metadata?.Path,
            metadata?.OperationId).Trim();
    }

    private static string BuildResourceToken(string path)
    {
        var resource = ResolveTargetResourceSegment(path, "id");
        return string.IsNullOrWhiteSpace(resource) ? null : resource;
    }

    private static string BuildPreferredVariableName(string token, string path)
    {
        if (!string.IsNullOrWhiteSpace(token) &&
            !string.Equals(token, "id", StringComparison.OrdinalIgnoreCase))
        {
            return token;
        }

        var resource = ResolveTargetResourceSegment(path, token);
        if (string.IsNullOrWhiteSpace(resource))
        {
            return token;
        }

        var identifier = ToCamelIdentifier(resource);
        return string.IsNullOrWhiteSpace(identifier) ? token : $"{identifier}Id";
    }

    private static string ResolveTargetResourceSegment(string path, string token)
    {
        var segments = (path ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        for (int i = 0; i < segments.Count; i++)
        {
            if (!string.Equals(segments[i], $"{{{token}}}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (int j = i - 1; j >= 0; j--)
            {
                var cleaned = CleanSegment(segments[j]);
                if (string.IsNullOrWhiteSpace(cleaned) ||
                    cleaned.Equals("api", StringComparison.OrdinalIgnoreCase) ||
                    IsVersionPrefix(cleaned))
                {
                    continue;
                }

                return Singularize(cleaned);
            }
        }

        var strippedToken = StripIdSuffix(token);
        if (!string.IsNullOrWhiteSpace(strippedToken))
        {
            return Singularize(strippedToken);
        }

        for (int i = segments.Count - 1; i >= 0; i--)
        {
            var cleaned = CleanSegment(segments[i]);
            if (string.IsNullOrWhiteSpace(cleaned) ||
                cleaned.Equals("api", StringComparison.OrdinalIgnoreCase) ||
                IsVersionPrefix(cleaned) ||
                (cleaned.StartsWith("{", StringComparison.Ordinal) && cleaned.EndsWith("}", StringComparison.Ordinal)))
            {
                continue;
            }

            return Singularize(cleaned);
        }

        return null;
    }

    private static string StripIdSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.EndsWith("Ids", StringComparison.OrdinalIgnoreCase) && value.Length > 3)
        {
            return value[..^3];
        }

        if (value.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && value.Length > 2)
        {
            return value[..^2];
        }

        return value;
    }

    private static string Singularize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && value.Length > 3)
        {
            return value[..^3] + "y";
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !value.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            value.Length > 1)
        {
            return value[..^1];
        }

        return value;
    }

    private static bool IsVersionPrefix(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 3 &&
            value.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
            value.Skip(1).All(char.IsDigit);
    }

    private static string CleanSegment(string segment)
    {
        return string.IsNullOrWhiteSpace(segment)
            ? segment
            : segment.Trim().Trim('/').Trim();
    }

    private static string ToCamelIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var parts = value
            .Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (parts.Count == 0)
        {
            return value;
        }

        var first = parts[0].Length == 0
            ? string.Empty
            : char.ToLowerInvariant(parts[0][0]) + parts[0][1..];

        var rest = parts
            .Skip(1)
            .Select(part => part.Length == 0
                ? string.Empty
                : char.ToUpperInvariant(part[0]) + part[1..]);

        return first + string.Concat(rest);
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

    private sealed class EndpointRequestContract
    {
        public EndpointRequestContract(
            HashSet<string> requiredPathParams,
            HashSet<string> requiredQueryParams,
            bool requiresBody,
            ContractAwareRequestContext requestContext)
        {
            RequiredPathParams = requiredPathParams ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RequiredQueryParams = requiredQueryParams ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RequiresBody = requiresBody;
            RequestContext = requestContext ?? new ContractAwareRequestContext();
        }

        public HashSet<string> RequiredPathParams { get; }

        public HashSet<string> RequiredQueryParams { get; }

        public bool RequiresBody { get; }

        public ContractAwareRequestContext RequestContext { get; }
    }
}
