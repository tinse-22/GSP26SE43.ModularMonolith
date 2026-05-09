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
using System.Text.RegularExpressions;
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
    private const int LeanScenarioTargetPerEndpoint = 5;
    private const int StandardScenarioTargetPerEndpoint = 15;
    private const int MaxScenarioTargetPerBatch = 20;

    private const int MaxBusinessContextLength = 1200;
    private const int MaxFeedbackContextLength = 1200;
    private const int MaxSystemPromptLength = 1200;
    private const int MaxCombinedPromptLength = 5000;
    private const int MaxObservationPromptLength = 3000;
    private const int MaxConfirmationPromptLength = 1200;
    private const int MaxTaskInstructionLength = 5000;
    private const int MaxRulesLength = 4500;
    private const int MaxResponseFormatLength = 2500;
    private const int MaxSchemaPayloadCountPerKind = 2;
    private const int MaxSchemaPayloadLength = 2500;
    private const int MaxParameterDetailCount = 16;
    private const int MaxParameterDefaultValueLength = 200;

    private const string DefaultSuggestionSystemPrompt =
        "You are a senior QA engineer specialising in REST API security and robustness testing. " +
        "Generate happy-path, boundary, and negative test scenarios for REST API endpoints and return ONLY raw JSON.";

    private const string SuggestionRulesBlock =
        "=== RULES ===\n" +
        "1. Generate scenarios by HTTP method: GET and DELETE endpoints need exactly 5 scenarios total; POST, PUT, and PATCH endpoints need exactly 15 scenarios total; other methods default to 15 scenarios. Always include at least one HappyPath when endpoint is executable, plus Boundary/Negative where applicable.\n" +
        "   - For GET/DELETE, include the 5 highest-value checks covering happy-path, boundary, and negative cases.\n" +
        "2. HappyPath: valid request payload and expected success status (2xx) with realistic data.\n" +
        "3. Boundary: values at the edge of valid range (e.g. empty string, max length, 0, -1, very large number).\n" +
        "3b. If the boundary value equals a valid minimum/maximum per SRS, expectedStatus must be 2xx (success). Use 4xx only when the constraint is violated or when SRS explicitly says the boundary value fails.\n" +
        "4. Negative: invalid type, missing required field, wrong auth, forbidden access, not found.\n" +
        "5. DYNAMIC DATA — MANDATORY rules for all test types and all API domains:\n" +
        "   a) UNIQUENESS: For ANY field that must be unique per run (email, username, code, slug, phone, name, sku, ref, etc.), embed {{tcUniqueId}} in the value. " +
        "The BE resolves {{tcUniqueId}} to a fresh 8-char hex per execution. " +
        "Use regardless of API type: email → \"user_{{tcUniqueId}}@yourdomain.com\", username → \"user_{{tcUniqueId}}\", productCode → \"PROD_{{tcUniqueId}}\". " +
        "NEVER invent your own suffixes — always use {{tcUniqueId}}.\n" +
        "   b) CROSS-TEST VARIABLES: The execution engine AUTOMATICALLY extracts ALL primitive fields from every successful (2xx) POST/PUT/PATCH request body into a shared variable bag, keyed by exact field name. " +
        "Downstream tests reference them as {{fieldName}} directly. " +
        "If a prior register test sent {email, password, name}, then {{email}}, {{password}}, {{name}} are all available. " +
        "If a prior create-product test sent {name, price}, then {{name}}, {{price}} are available. " +
        "Semantic aliases: {{registeredEmail}} = {{email}} from first register, {{registeredPassword}} = {{password}} from first register.\n" +
        "   c) RESPONSE IDs: Engine also auto-extracts IDs from successful response bodies (e.g. {{productId}}, {{orderId}} from $.data.id). Add explicit 'variables' rules only when you need a non-standard path.\n" +
        "   d) FLOW CHAINS (apply to ALL API types, not just auth):\n" +
        "      - HappyPath create/register: use {{tcUniqueId}} for all unique fields. No explicit variable rules needed for primitive request body fields.\n" +
        "      - HappyPath login/get/update: use {{fieldName}} from the prior successful test (e.g. {{email}}, {{name}}, {{productId}}). Do NOT hardcode values.\n" +
        "      - Negative 'duplicate value' tests: use {{<fieldName>}} matching the conflicting field (e.g. {{email}} for duplicate email, {{name}} for duplicate product name) — NOT a new unique value.\n" +
        "      - Negative 'not found' tests: use \"nonexistent_{{tcUniqueId}}\" to guarantee the value does not exist.\n" +
        "      - Tests needing a prior resource ID: use {{<resource>Id}} (e.g. {{productId}}, {{orderId}}, {{userId}}).\n" +
        "6. endpointId must be the EXACT UUID from input.\n" +
        "7. testType must be exactly \"HappyPath\", \"Boundary\", or \"Negative\".\n" +
        "8. priority: \"High\" for auth/security issues, \"Medium\" for validation, \"Low\" for edge cases.\n" +
        "9. Respect endpoint contract strictly: preserve real parameter names and locations (path/query/header/body).\n" +
        "10. If endpoint has required path params, request.pathParams MUST include non-empty values for every required token.\n" +
        "11. If endpoint has required query params, request.queryParams MUST include non-empty values for every required query param.\n" +
        "12. If endpoint requires request body, request.bodyType must be one of JSON, FormData, UrlEncoded, or Raw as appropriate for the contract, and request.body must be non-empty.\n" +
        "13. Treat expectation as a CANDIDATE oracle only. Propose the best expectedStatus/bodyContains/bodyNotContains/jsonPathChecks/headerChecks you can infer from contract + SRS, but backend will reconcile the final authoritative expectation.\n" +
        "14. EXPECTATION TOKENS (GENERALIZED): For jsonPathChecks values, use canonical tokens only: \"present\", \"not null\", \"non-empty\", \"string\", \"number\", \"boolean\", \"array\", \"object\", \"uuid\", \"datetime\", or regex:<pattern>. Avoid camelCase tokens like nonEmpty/notEmpty. Do not assert full message strings or session-specific values (token/id/timestamp); use existence/type/regex instead.\n" +
        "15. AUTH MODE: For Unauthorized/Missing Token tests, set request.headers to include \"X-Test-Auth-Mode\": \"none\" and do NOT send Authorization. For Invalid Token tests, set Authorization to an invalid value and do NOT set X-Test-Auth-Mode.\n" +
        "16. For HappyPath, propose 1-3 bodyContains substrings and 1-2 JSONPath assertions on critical success fields when the response contract or SRS makes them inferable. Prefer contract-backed field names; use \"*\" only to assert existence.\n" +
        "17. For Boundary/Negative, propose 1-2 bodyContains substrings and 1 JSONPath assertion on the error payload when inferable. Prefer keywords and fields grounded in SRS constraints or errorResponses schema; avoid invented fields.\n" +
        "17b. If errorResponses[statusCode].schemaJson is provided, derive candidate bodyContains/jsonPathChecks from that schema instead of free-form guessing.\n" +
        "18. SRS-CONSTRAINT-DRIVEN: When srsContext.requirements[n].testableConstraints is non-empty, generate at least 1 scenario per meaningful constraint item. Rules:\n" +
        "   - The scenario's endpointId MUST be requirements[n].endpointId (if not null).\n" +
        "   - coveredRequirementCodes MUST include requirements[n].code.\n" +
        "   - expectedStatus/bodyContains/jsonPathChecks should mirror the constraint's expectedOutcome and wording as closely as possible.\n" +
        "   - Example: constraint='password >= 6 chars → 400' → Boundary test, body={password:'12345'}, expectedStatus=[400], bodyContains=['password','minimum'].\n" +
        "   - If no testableConstraints, ignore this rule.\n";

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
        "        \"bodyType\": \"None|JSON|FormData|UrlEncoded|Raw\",\n" +
        "        \"body\": \"<serialized request payload or null>\",\n" +
        "        \"timeout\": 30000\n" +
        "      },\n" +
        "      \"expectation\": {\n" +
        "        \"expectedStatus\": [400],\n" +
        "        \"bodyContains\": [\"error\", \"required\"],\n" +
        "        \"bodyNotContains\": null,\n" +
        "        \"jsonPathChecks\": {\"$.success\": \"false\"}\n" +
        "      },\n" +
        "      \"coveredRequirementCodes\": [\"REQ-001\"],\n" +
        "      \"variables\": []\n" +
        "    }\n" +
        "  ],\n" +
        "  \"model\": \"<model name>\",\n" +
        "  \"tokensUsed\": 0\n" +
        "}";

    private static readonly Regex EmailLiteralRegex = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
    private readonly IExpectationResolver _expectationResolver;
    private readonly ILogger<LlmScenarioSuggester> _logger;

    public LlmScenarioSuggester(
        IObservationConfirmationPromptBuilder promptBuilder,
        IN8nIntegrationService n8nService,
        ILlmAssistantGatewayService llmGatewayService,
        ILlmSuggestionFeedbackContextService feedbackContextService,
        IExpectationResolver expectationResolver,
        ILogger<LlmScenarioSuggester> logger)
    {
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _n8nService = n8nService ?? throw new ArgumentNullException(nameof(n8nService));
        _llmGatewayService = llmGatewayService ?? throw new ArgumentNullException(nameof(llmGatewayService));
        _feedbackContextService = feedbackContextService ?? throw new ArgumentNullException(nameof(feedbackContextService));
        _expectationResolver = expectationResolver ?? throw new ArgumentNullException(nameof(expectationResolver));
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

        var useCacheLookup = false;
        _logger.LogInformation(
            "Skipping LLM suggestion cache lookup. Fresh n8n call is enforced for every generate request. TestSuiteId={TestSuiteId}, BypassCache={BypassCache}, UseFeedbackLoopContext={UseFeedbackLoopContext}",
            context.TestSuiteId,
            context.BypassCache,
            algorithmProfile.UseFeedbackLoopContext);

        // Step 1: Check cache for each endpoint
        var cacheKey = BuildCacheKey(context, orderedSequence, feedbackContext.FeedbackFingerprint);
        _logger.LogInformation(
            "Prepared LLM suggestion cache input. TestSuiteId={TestSuiteId}, FeedbackFingerprint={FeedbackFingerprint}, CacheKey={CacheKey}, EndpointCount={EndpointCount}",
            context.TestSuiteId,
            feedbackContext.FeedbackFingerprint,
            cacheKey,
            orderedSequence.Count);
        var cachedResult = useCacheLookup
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

        // Step 2: Build prompts and call n8n in batches to avoid output truncation.
        var metadataMap = context.EndpointMetadata.ToDictionary(e => e.EndpointId);
        var endpointBatches = BuildEndpointBatches(orderedSequence, metadataMap);

        var allScenarios = new List<LlmSuggestedScenario>();
        var totalTokens = 0;
        var totalLatencyMs = 0;
        var usedLocalFallback = false;
        string modelUsed = null;

        for (var batchIndex = 0; batchIndex < endpointBatches.Count; batchIndex++)
        {
            var batch = endpointBatches[batchIndex];
            var orderedMetadata = batch
                .Where(oe => metadataMap.ContainsKey(oe.EndpointId))
                .Select(oe => metadataMap[oe.EndpointId])
                .ToList();

            IReadOnlyList<ObservationConfirmationPrompt> prompts = Array.Empty<ObservationConfirmationPrompt>();
            if (algorithmProfile.UseObservationConfirmationPrompting)
            {
                var promptContexts = EndpointPromptContextMapper.Map(orderedMetadata, context.Suite);
                prompts = _promptBuilder.BuildForSequence(promptContexts);
            }

            var payload = BuildN8nPayload(
                context,
                batch,
                metadataMap,
                prompts,
                feedbackContext.EndpointFeedbackContexts);
            var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts).Length;

            _logger.LogInformation(
                "Calling n8n webhook '{WebhookName}' for boundary/negative scenario suggestion. TestSuiteId={TestSuiteId}, Batch={BatchIndex}/{BatchCount}, EndpointCount={EndpointCount}, PayloadBytes={PayloadBytes}, FeedbackFingerprint={FeedbackFingerprint}, CacheKey={CacheKey}",
                N8nWebhookNames.GenerateLlmSuggestions,
                context.TestSuiteId,
                batchIndex + 1,
                endpointBatches.Count,
                batch.Count,
                payloadBytes,
                feedbackContext.FeedbackFingerprint,
                cacheKey);

            var stopwatch = Stopwatch.StartNew();
            N8nBoundaryNegativeResponse n8nResponse;

            try
            {
                n8nResponse = await _n8nService.TriggerWebhookAsync<N8nBoundaryNegativePayload, N8nBoundaryNegativeResponse>(
                    N8nWebhookNames.GenerateLlmSuggestions, payload, cancellationToken);
            }
            catch (N8nTransientException ex)
            {
                usedLocalFallback = true;
                _logger.LogWarning(
                    ex,
                    "Transient n8n failure while generating LLM suggestions. Falling back to local contract-based synthesis. TestSuiteId={TestSuiteId}, Batch={BatchIndex}/{BatchCount}, EndpointCount={EndpointCount}, StatusCode={StatusCode}, Webhook={Webhook}",
                    context.TestSuiteId,
                    batchIndex + 1,
                    endpointBatches.Count,
                    batch.Count,
                    ex.StatusCode,
                    N8nWebhookNames.GenerateLlmSuggestions);

                n8nResponse = new N8nBoundaryNegativeResponse
                {
                    Scenarios = new List<N8nSuggestedScenario>(),
                    Model = "local-fallback",
                    TokensUsed = 0,
                };
            }

            stopwatch.Stop();
            var latencyMs = (int)stopwatch.ElapsedMilliseconds;

            await SaveInteractionAsync(context, payload, n8nResponse, latencyMs, cancellationToken);

            var batchScenarios = ParseScenarios(n8nResponse, context.SrsRequirements);
            allScenarios.AddRange(batchScenarios);

            if (algorithmProfile.UseFeedbackLoopContext)
            {
                await CacheResultsAsync(context, batch, cacheKey, batchScenarios, cancellationToken);
            }

            totalLatencyMs += latencyMs;
            totalTokens += n8nResponse?.TokensUsed ?? 0;
            if (string.IsNullOrWhiteSpace(modelUsed) && !string.IsNullOrWhiteSpace(n8nResponse?.Model))
            {
                modelUsed = n8nResponse.Model;
            }

            _logger.LogInformation(
                "LLM scenario batch complete. TestSuiteId={TestSuiteId}, Batch={BatchIndex}/{BatchCount}, ScenarioCount={Count}, Model={Model}, TokensUsed={Tokens}, LatencyMs={LatencyMs}",
                context.TestSuiteId,
                batchIndex + 1,
                endpointBatches.Count,
                batchScenarios.Count,
                n8nResponse?.Model,
                n8nResponse?.TokensUsed,
                latencyMs);
        }

        _logger.LogInformation(
            "LLM scenario suggestion complete. TestSuiteId={TestSuiteId}, ScenarioCount={Count}, Model={Model}, TokensUsed={Tokens}, LatencyMs={LatencyMs}, FeedbackFingerprint={FeedbackFingerprint}, CacheKey={CacheKey}",
            context.TestSuiteId,
            allScenarios.Count,
            modelUsed,
            totalTokens,
            totalLatencyMs,
            feedbackContext.FeedbackFingerprint,
            cacheKey);

        return new LlmScenarioSuggestionResult
        {
            Scenarios = allScenarios,
            LlmModel = modelUsed,
            TokensUsed = totalTokens,
            LatencyMs = totalLatencyMs,
            FromCache = false,
            UsedLocalFallback = usedLocalFallback,
        };
    }

    private static List<List<ApiOrderItemModel>> BuildEndpointBatches(
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        var batches = new List<List<ApiOrderItemModel>>();
        if (orderedEndpoints == null || orderedEndpoints.Count == 0)
        {
            return batches;
        }

        var current = new List<ApiOrderItemModel>();
        var currentTarget = 0;

        foreach (var endpoint in orderedEndpoints)
        {
            ApiEndpointMetadataDto metadata = null;
            if (metadataMap != null)
            {
                metadataMap.TryGetValue(endpoint.EndpointId, out metadata);
            }
            var target = ComputeAdaptiveScenarioTarget(endpoint, metadata);

            if (current.Count > 0 && currentTarget + target > MaxScenarioTargetPerBatch)
            {
                batches.Add(current);
                current = new List<ApiOrderItemModel>();
                currentTarget = 0;
            }

            current.Add(endpoint);
            currentTarget += target;
        }

        if (current.Count > 0)
        {
            batches.Add(current);
        }

        return batches;
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
                BusinessContext = TruncateForPayload(businessContext, MaxBusinessContextLength),
                FeedbackContext = endpointFeedbackContexts.TryGetValue(orderItem.EndpointId, out var feedbackContext)
                    ? TruncateForPayload(feedbackContext, MaxFeedbackContextLength)
                    : string.Empty,
                Prompt = promptPayload,
                ParameterSchemaPayloads = CompactSchemaPayloads(metadata?.ParameterSchemaPayloads),
                ResponseSchemaPayloads = CompactSchemaPayloads(metadata?.ResponseSchemaPayloads),
                ParameterDetails = BuildParameterDetails(context, orderItem.EndpointId),
                ErrorResponses = BuildErrorResponseDescriptors(metadata?.Responses),
            });
        }

        return new N8nBoundaryNegativePayload
        {
            TestSuiteId = context.TestSuiteId,
            TestSuiteName = context.Suite.Name,
            GlobalBusinessRules = TruncateForPayload(context.Suite.GlobalBusinessRules, MaxBusinessContextLength),
            SrsContext = BuildSrsContext(context),
            AlgorithmProfile = context.AlgorithmProfile ?? new GenerationAlgorithmProfile(),
            PromptConfig = BuildSuggestionPromptConfig(context, prompts, orderedEndpoints, metadataMap),
            Endpoints = endpointPayloads,
        };
    }

    private static N8nSrsContext BuildSrsContext(LlmScenarioSuggestionContext context)
    {
        if (context.SrsDocument == null)
            return null;

        var content = !string.IsNullOrWhiteSpace(context.SrsDocument.ParsedMarkdown)
            ? context.SrsDocument.ParsedMarkdown
            : context.SrsDocument.RawContent;

        // Truncate to avoid token overflow (~12 000 chars ≈ ~3 000 tokens)
        const int MaxSrsContentLength = 12_000;
        if (content?.Length > MaxSrsContentLength)
            content = content.Substring(0, MaxSrsContentLength) + "\n...[truncated]";

        var requirements = context.SrsRequirements
            .Select(r => new N8nSrsRequirementBrief
            {
                Code = r.RequirementCode,
                Title = r.Title,
                Description = TruncateForPayload(r.Description, 400),
                EndpointId = r.EndpointId,
                TestableConstraints = DeserializeTestableConstraints(
                    r.RefinedConstraints ?? r.TestableConstraints),
            })
            .ToList();

        return new N8nSrsContext
        {
            DocumentTitle = context.SrsDocument.Title,
            Content = content,
            Requirements = requirements,
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
            SystemPrompt = TruncateForPayload(string.IsNullOrWhiteSpace(prompt?.SystemPrompt)
                ? DefaultSuggestionSystemPrompt
                : prompt.SystemPrompt, MaxSystemPromptLength),
            CombinedPrompt = TruncateForPayload(combinedPrompt, MaxCombinedPromptLength),
            ObservationPrompt = TruncateForPayload(string.IsNullOrWhiteSpace(prompt?.ObservationPrompt)
                ? combinedPrompt
                : prompt.ObservationPrompt, MaxObservationPromptLength),
            ConfirmationPromptTemplate = TruncateForPayload(string.IsNullOrWhiteSpace(prompt?.ConfirmationPromptTemplate)
                ? "Generate only boundary/negative scenarios based on provided API details and business rules; return JSON only."
                : prompt.ConfirmationPromptTemplate, MaxConfirmationPromptLength),
        };
    }

    private static string BuildFallbackCombinedPrompt(
        ApiOrderItemModel orderItem,
        TestSuite suite,
        ApiEndpointMetadataDto metadata,
        string businessContext)
    {
        var target = ComputeAdaptiveScenarioTarget(orderItem, metadata);
        var sb = new StringBuilder();
        sb.AppendLine("# Endpoint Context (Fallback)");
        sb.AppendLine($"Method: {orderItem?.HttpMethod ?? metadata?.HttpMethod ?? "GET"}");
        sb.AppendLine($"Path: {orderItem?.Path ?? metadata?.Path ?? "/"}");
        sb.AppendLine($"OperationId: {metadata?.OperationId ?? "N/A"}");
        sb.AppendLine();
        sb.AppendLine("Generate boundary and negative scenarios for this endpoint only.");
        sb.AppendLine($"Target scenario count for this endpoint: {target} total scenario(s).");

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
            foreach (var schema in metadata.ParameterSchemaPayloads.Where(x => !string.IsNullOrWhiteSpace(x)).Take(2))
            {
                sb.AppendLine(schema);
            }
        }

        if (metadata?.ResponseSchemaPayloads?.Any() == true)
        {
            sb.AppendLine();
            sb.AppendLine("## Response Schemas");
            foreach (var schema in metadata.ResponseSchemaPayloads.Where(x => !string.IsNullOrWhiteSpace(x)).Take(2))
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
            SystemPrompt = TruncateForPayload(string.IsNullOrWhiteSpace(systemPrompt)
                ? DefaultSuggestionSystemPrompt
                : systemPrompt, MaxSystemPromptLength),
            TaskInstruction = TruncateForPayload(
                BuildSuggestionTaskInstruction(context?.Suite, orderedEndpoints, metadataMap),
                MaxTaskInstructionLength),
            Rules = TruncateForPayload(SuggestionRulesBlock, MaxRulesLength),
            ResponseFormat = TruncateForPayload(SuggestionResponseFormatBlock, MaxResponseFormatLength),
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
                    $"- [{endpoint.OrderIndex}] {endpoint.HttpMethod} {endpoint.Path}: target {target} scenarios, prioritize {expectedTypes}.");
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
        IReadOnlyList<SrsRequirement> srsRequirements = null)
    {
        if (response?.Scenarios == null || response.Scenarios.Count == 0)
        {
            return Array.Empty<LlmSuggestedScenario>();
        }

        // Build code → GUID lookup so LLM-returned codes ("REQ-001") can be resolved to DB IDs.
        var codeToId = srsRequirements != null
            ? srsRequirements
                .Where(r => !string.IsNullOrWhiteSpace(r.RequirementCode))
                .ToDictionary(r => r.RequirementCode.Trim(), r => r.Id, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var scenarios = new List<LlmSuggestedScenario>(response.Scenarios.Count);

        foreach (var s in response.Scenarios)
        {
            var parsedType = ParseTestType(s.TestType);

            // Map requirement codes returned by LLM → GUIDs from our DB.
            var coveredIds = s.CoveredRequirementCodes
                ?.Where(c => !string.IsNullOrWhiteSpace(c) && codeToId.ContainsKey(c.Trim()))
                .Select(c => codeToId[c.Trim()])
                .Distinct()
                .ToList()
                ?? new List<Guid>();

            var expectedStatuses = s.Expectation?.ExpectedStatus ?? new List<int>();
            var bodyContains = s.Expectation?.BodyContains;

            var parsedScenario = new LlmSuggestedScenario
            {
                EndpointId = s.EndpointId,
                ScenarioName = s.ScenarioName,
                Description = s.Description,
                SuggestedTestType = parsedType,
                SuggestedHttpMethod = s.Request?.HttpMethod,
                SuggestedUrl = s.Request?.Url,
                SuggestedBodyType = s.Request?.BodyType,
                SuggestedBody = s.Request?.Body,
                SuggestedPathParams = s.Request?.PathParams,
                SuggestedQueryParams = s.Request?.QueryParams,
                SuggestedHeaders = s.Request?.Headers,
                ExpectedStatusCode = expectedStatuses.FirstOrDefault(),
                ExpectedStatusCodes = expectedStatuses,
                ExpectedBehavior = bodyContains?.FirstOrDefault(),
                SuggestedBodyContains = bodyContains,
                SuggestedBodyNotContains = s.Expectation?.BodyNotContains,
                SuggestedJsonPathChecks = s.Expectation?.JsonPathChecks,
                SuggestedHeaderChecks = s.Expectation?.HeaderChecks,
                Priority = s.Priority,
                Tags = s.Tags ?? new List<string>(),
                Variables = s.Variables ?? new List<N8nTestCaseVariable>(),
                CoveredRequirementIds = coveredIds,
                ExpectationSource = s.Expectation?.ExpectationSource,
                RequirementCode = s.Expectation?.RequirementCode,
                PrimaryRequirementId = s.Expectation?.PrimaryRequirementId,
            };

            scenarios.Add(parsedScenario);
        }

        return scenarios;
    }

    private static List<string> NormalizeRegisterBodyContains(
        List<string> bodyContains,
        N8nTestCaseRequest request,
        string scenarioName)
    {
        if (bodyContains == null || bodyContains.Count == 0)
        {
            return bodyContains;
        }

        if (!IsRegisterLikeRequest(request?.HttpMethod, request?.Url, scenarioName))
        {
            return bodyContains;
        }

        var filtered = bodyContains
            .Where(value => !IsEmailLiteral(value))
            .ToList();

        return filtered.Count > 0 ? filtered : bodyContains;
    }

    private static bool IsRegisterLikeRequest(string httpMethod, string url, string name)
    {
        var signature = $"{httpMethod} {url} {name}";
        return signature.Contains("/register", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("/signup", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("/sign-up", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmailLiteral(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return EmailLiteralRegex.IsMatch(value);
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

    private IReadOnlyList<LlmSuggestedScenario> EnsureAdaptiveCoverage(
        IReadOnlyList<LlmSuggestedScenario> rawScenarios,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap,
        IReadOnlyDictionary<Guid, EndpointRequestContract> endpointContracts,
        IReadOnlyList<SrsRequirement> srsRequirements)
    {
        if (orderedEndpoints == null || orderedEndpoints.Count == 0)
        {
            return rawScenarios ?? Array.Empty<LlmSuggestedScenario>();
        }

        // Only order the LLM-returned scenarios by endpoint order — no synthetic padding.
        var byEndpoint = (rawScenarios ?? Array.Empty<LlmSuggestedScenario>())
            .GroupBy(x => x.EndpointId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return orderedEndpoints
            .OrderBy(x => x.OrderIndex)
            .SelectMany(x => byEndpoint.TryGetValue(x.EndpointId, out var list)
                ? list
                : Enumerable.Empty<LlmSuggestedScenario>())
            .ToList();
    }

    private static int ComputeAdaptiveScenarioTarget(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        var method = ResolveHttpMethod(endpoint, metadata);
        return IsLeanScenarioMethod(method)
            ? LeanScenarioTargetPerEndpoint
            : StandardScenarioTargetPerEndpoint;
    }

    private static string ResolveHttpMethod(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        return (endpoint?.HttpMethod ?? metadata?.HttpMethod ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static bool IsLeanScenarioMethod(string method)
    {
        return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
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

    private LlmSuggestedScenario CreateFallbackScenario(
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata,
        EndpointRequestContract contract,
        TestType type,
        int index,
        IReadOnlyList<SrsRequirement> srsRequirements)
    {
        var method = endpoint?.HttpMethod ?? metadata?.HttpMethod ?? "GET";
        var path = endpoint?.Path ?? metadata?.Path ?? "/";

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

        var resolvedExpectation = _expectationResolver.Resolve(new GeneratedScenarioContext
        {
            EndpointId = endpoint.EndpointId,
            TestType = type,
            HttpMethod = method,
            SwaggerResponses = metadata?.Responses ?? Array.Empty<ApiEndpointResponseDescriptorDto>(),
            SrsRequirements = srsRequirements ?? Array.Empty<SrsRequirement>(),
        });

        var expectedStatuses = resolvedExpectation?.ExpectedStatusCodes ?? new List<int> { 200 };

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
            ExpectedBehavior = resolvedExpectation?.BodyContains?.FirstOrDefault(),
            SuggestedBodyContains = resolvedExpectation?.BodyContains,
            SuggestedBodyNotContains = resolvedExpectation?.BodyNotContains,
            SuggestedJsonPathChecks = resolvedExpectation?.JsonPathChecks,
            SuggestedHeaderChecks = resolvedExpectation?.HeaderChecks,
            Priority = type == TestType.HappyPath ? "Medium" : "High",
            Tags = tags,
            Variables = requestData.Variables,
            ExpectationSource = (resolvedExpectation?.Source ?? Models.ExpectationSource.Default).ToString(),
            RequirementCode = resolvedExpectation?.RequirementCode,
            PrimaryRequirementId = resolvedExpectation?.PrimaryRequirementId,
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
                    if (string.IsNullOrWhiteSpace(parameter.Name))
                    {
                        continue;
                    }

                    if (string.Equals(parameter.Location, "Path", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parameter.IsRequired)
                        {
                            requiredPathParams.Add(parameter.Name);
                        }
                    }
                    else if (string.Equals(parameter.Location, "Query", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parameter.IsRequired)
                        {
                            requiredQueryParams.Add(parameter.Name);
                        }
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
        if (!string.IsNullOrWhiteSpace(currentResourcePlaceholder) &&
            HasRouteToken(endpointPath, "id"))
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

    private static bool HasRouteToken(string path, string token)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => string.Equals(segment, $"{{{token}}}", StringComparison.OrdinalIgnoreCase));
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
        if (!string.IsNullOrWhiteSpace(strippedToken) &&
            !string.Equals(strippedToken, "id", StringComparison.OrdinalIgnoreCase))
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

        return detail.Parameters
            .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))
            .OrderByDescending(p => p.IsRequired)
            .ThenBy(p => p.Location)
            .ThenBy(p => p.Name)
            .Take(MaxParameterDetailCount)
            .Select(p => new N8nParameterDetail
            {
                Name = p.Name,
                Location = p.Location,
                DataType = p.DataType,
                Format = p.Format,
                IsRequired = p.IsRequired,
                DefaultValue = TruncateForPayload(p.DefaultValue, MaxParameterDefaultValueLength),
            })
            .ToList();
    }

    private static List<string> CompactSchemaPayloads(IEnumerable<string> schemaPayloads)
    {
        if (schemaPayloads == null)
        {
            return new List<string>();
        }

        return schemaPayloads
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => TruncateForPayload(x, MaxSchemaPayloadLength))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxSchemaPayloadCountPerKind)
            .ToList();
    }

    /// <summary>
    /// Builds error response descriptors from Swagger metadata for inclusion in the n8n payload.
    /// Only includes 4xx/5xx responses. Max 5 entries to keep payload token count manageable.
    /// </summary>
    private static Dictionary<string, N8nErrorResponseDescriptor> BuildErrorResponseDescriptors(
        IReadOnlyCollection<ApiEndpointResponseDescriptorDto> responses)
    {
        if (responses == null || responses.Count == 0)
            return new Dictionary<string, N8nErrorResponseDescriptor>();

        return responses
            .Where(r => r.StatusCode >= 400 && r.StatusCode <= 599)
            .OrderBy(r => r.StatusCode)
            .Take(5)
            .ToDictionary(
                r => r.StatusCode.ToString(),
                r => new N8nErrorResponseDescriptor
                {
                    Description = r.Description,
                    SchemaJson = TruncateForPayload(r.Schema, 800),
                    ExampleJson = TruncateForPayload(r.Examples, 400),
                });
    }

    /// <summary>
    /// Repairs scenario assertions from Swagger error response schema when LLM left them empty.
    /// Only fills when LLM produced no assertions (Count == 0) — never overwrites LLM results.
    ///
    /// WARNING: This method depends on metadata.Responses being in sync with the runtime API.
    /// If the Swagger spec is outdated (API changed fields but spec not regenerated),
    /// this will produce assertions based on stale schema data, causing false test failures.
    /// Team MUST ensure Swagger spec is regenerated before triggering test generation.
    /// </summary>
    private static LlmSuggestedScenario RepairAssertionsFromSchema(
        LlmSuggestedScenario scenario,
        IReadOnlyCollection<ApiEndpointResponseDescriptorDto> swaggerResponses)
    {
        if (swaggerResponses == null || swaggerResponses.Count == 0) return scenario;

        // Find the matching Swagger response for this scenario's primary expected code
        var primaryCode = scenario.ExpectedStatusCodes?.FirstOrDefault()
            ?? scenario.ExpectedStatusCode;

        var matchingResponse = swaggerResponses.FirstOrDefault(r => r.StatusCode == primaryCode)
            ?? swaggerResponses.FirstOrDefault(r => r.StatusCode >= 400 && r.StatusCode < 500);

        if (matchingResponse == null) return scenario;

        // Only repair if LLM left empty
        if ((scenario.SuggestedJsonPathChecks == null || scenario.SuggestedJsonPathChecks.Count == 0)
            && !string.IsNullOrWhiteSpace(matchingResponse.Schema))
        {
            scenario.SuggestedJsonPathChecks = ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(
                matchingResponse.Schema, scenario.SuggestedTestType);
        }

        if ((scenario.SuggestedBodyContains == null || scenario.SuggestedBodyContains.Count == 0)
            && !string.IsNullOrWhiteSpace(matchingResponse.Schema))
        {
            scenario.SuggestedBodyContains = ErrorResponseSchemaAnalyzer.ExtractFieldNames(matchingResponse.Schema);
        }

        return scenario;
    }

    /// <summary>
    /// Deserializes TestableConstraints JSON from SrsRequirement into structured briefs.
    /// Format: [{"constraint": "password >= 6 → 400", "priority": "High"}, ...]
    /// Returns max 5 constraints per requirement to keep payload compact.
    /// </summary>
    private static List<SrsTestableConstraintBrief> DeserializeTestableConstraints(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<SrsTestableConstraintBrief>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new List<SrsTestableConstraintBrief>();

            var result = new List<SrsTestableConstraintBrief>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var constraint = item.TryGetProperty("constraint", out var c) ? c.GetString() : null;
                if (string.IsNullOrWhiteSpace(constraint)) continue;

                var priority = item.TryGetProperty("priority", out var p) ? p.GetString() : "Medium";
                var outcome = ExtractExpectedOutcome(constraint);

                result.Add(new SrsTestableConstraintBrief
                {
                    Constraint = TruncateForPayload(constraint, 200),
                    ExpectedOutcome = outcome,
                    Priority = priority,
                });
            }
            return result.Take(5).ToList();
        }
        catch { return new List<SrsTestableConstraintBrief>(); }
    }

    /// <summary>
    /// Heuristic: extracts expected outcome from constraint text containing "→" or "-&gt;" arrow.
    /// Example: "password >= 6 chars → 400" extracts "400".
    /// </summary>
    private static string ExtractExpectedOutcome(string constraintText)
    {
        if (string.IsNullOrWhiteSpace(constraintText)) return null;
        var arrowIdx = constraintText.IndexOf('→');
        if (arrowIdx < 0) arrowIdx = constraintText.IndexOf("->", StringComparison.Ordinal);
        if (arrowIdx >= 0 && arrowIdx < constraintText.Length - 1)
            return constraintText[(arrowIdx + 1)..].Trim();
        return null;
    }

    private static string TruncateForPayload(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength];
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

        // Include suite-level business rules so config edits invalidate cache.
        sb.Append(NormalizeCacheText(context.Suite?.GlobalBusinessRules));
        sb.Append(':');

        // Include endpoint-level contexts in the same execution order.
        sb.Append(BuildEndpointBusinessContextSignature(context.Suite, orderedEndpoints));
        sb.Append(':');

        // Include algorithm switches that affect generation behavior.
        sb.Append(BuildAlgorithmProfileSignature(context.AlgorithmProfile));
        sb.Append(':');

        foreach (var ep in orderedEndpoints)
        {
            sb.Append(ep.OrderIndex).Append('|');
            sb.Append(ep.EndpointId).Append(',');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash)[..16]; // 8-byte hex prefix
    }

    private static string BuildEndpointBusinessContextSignature(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints)
    {
        if (suite?.EndpointBusinessContexts == null
            || suite.EndpointBusinessContexts.Count == 0
            || orderedEndpoints == null
            || orderedEndpoints.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var endpoint in orderedEndpoints)
        {
            suite.EndpointBusinessContexts.TryGetValue(endpoint.EndpointId, out var endpointContext);
            if (string.IsNullOrWhiteSpace(endpointContext))
            {
                continue;
            }

            sb.Append(endpoint.EndpointId)
                .Append('=')
                .Append(NormalizeCacheText(endpointContext))
                .Append(';');
        }

        return sb.ToString();
    }

    private static string BuildAlgorithmProfileSignature(GenerationAlgorithmProfile profile)
    {
        profile ??= new GenerationAlgorithmProfile();

        return string.Concat(
            profile.UseObservationConfirmationPrompting ? '1' : '0',
            profile.UseDependencyAwareOrdering ? '1' : '0',
            profile.UseSchemaRelationshipAnalysis ? '1' : '0',
            profile.UseSemanticTokenMatching ? '1' : '0',
            profile.UseFeedbackLoopContext ? '1' : '0');
    }

    private static string NormalizeCacheText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
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
