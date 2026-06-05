using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.DTOs;
using ClassifiedAds.Contracts.LlmAssistant.Services;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private const string ScenarioQualityPolicyVersion = "scenario-quality-v2";

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
        "1. Use the provided per-endpoint scenarioBudget. Treat softLimit/target as the preferred budget. Do not exceed hardLimit. Always include at least one HappyPath when endpoint is executable, plus Boundary/Negative where applicable.\n" +
        "   - Do not pad weak, duplicate, or near-duplicate variants just to reach a count. Fewer strong scenarios are better than repeated data.\n" +
        "   - Each kept scenario must cover a distinct field, constraint, status, auth, resource-not-found, or business-rule dimension.\n" +
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
        "      - HappyPath login/get/update: use {{fieldName}} from the prior successful test (e.g. {{email}}, {{password}}, {{name}}, {{productId}}). Do NOT hardcode credentials or resource IDs.\n" +
        "      - Negative 'duplicate value' tests: use {{<fieldName>}} matching the conflicting field (e.g. {{email}} for duplicate email, {{name}} for duplicate product name) — NOT a new unique value.\n" +
        "      - Negative 'not found' tests: use \"nonexistent_{{tcUniqueId}}\" to guarantee the value does not exist.\n" +
        "      - Tests needing a prior resource ID: use {{<resource>Id}} (e.g. {{productId}}, {{orderId}}, {{userId}}).\n" +
        "   e) PRODUCER-CONSUMER CONTRACT: Never emit a consume variable unless it is produced by an earlier scenario in the same suite, produced by explicit variables on a successful scenario, or is a runtime variable such as {{tcUniqueId}}. " +
        "For auth-required happy paths, include or depend on a login/token success scenario that extracts authToken from the response using the contract-backed token field, for example $.token or $.accessToken. " +
        "For successful POST/PUT/PATCH resource creation, add variables for response IDs using the semantic resource name, for example categoryId/productId/orderId from $.id or $.data.id when inferable. " +
        "Set executionHints.produces, executionHints.consumes, and executionHints.dependsOn consistently so every non-runtime consume has a producer.\n" +
        "6. endpointId must be the EXACT UUID from the same input endpoint as request.httpMethod + request.url. Never reuse an endpointId from a different route, even when scenarios are in the same auth/resource flow.\n" +
        "7. testType must be exactly \"HappyPath\", \"Boundary\", or \"Negative\".\n" +
        "8. priority: \"High\" for auth/security issues, \"Medium\" for validation, \"Low\" for edge cases.\n" +
        "9. Respect endpoint contract strictly: preserve real parameter names and locations (path/query/header/body).\n" +
        "10. If endpoint has required path params, request.pathParams MUST include non-empty values for every required token.\n" +
        "11. If endpoint has required query params, request.queryParams MUST include non-empty values for every required query param.\n" +
        "12. If endpoint requires request body, request.bodyType must be one of JSON, FormData, UrlEncoded, or Raw as appropriate for the contract, and request.body must be non-empty.\n" +
        "12b. REQUEST SCHEMA TYPES: For OpenAPI body fields with type number/integer, emit JSON numeric literals or numeric-semantic placeholders only, such as {{price}}, {{stock}}, {{quantity}}, or {{count}}. Never put ID placeholders like {{productId}}, {{categoryId}}, or {{id}} into numeric fields, and never quote numeric literals for success cases.\n" +
        "12c. PLACEHOLDER SEMANTICS: Resource ID placeholders must match the target resource field. For example categoryId may use {{categoryId}}, but must not use {{productId}}. Boundary strings must be concrete JSON strings, not JavaScript expressions such as \"x\".repeat(100).\n" +
        "12d. NEGATIVE INTENT: Negative login/auth tests must actually mutate the credential or auth header under test. Do not reuse both {{registeredEmail}} and {{registeredPassword}} unchanged while expecting failure. Use 401 only for missing/invalid authentication; use 400/404/409 for validation, not-found, or conflict cases.\n" +
        "13. Treat expectation as a CANDIDATE oracle only. Propose the best expectedStatus/bodyContains/bodyNotContains/jsonPathChecks/headerChecks you can infer from contract + SRS, but backend will reconcile the final authoritative expectation.\n" +
        "14. EXPECTATION TOKENS (GENERALIZED): For jsonPathChecks values, use canonical tokens only: \"present\", \"not null\", \"non-empty\", \"string\", \"number\", \"boolean\", \"array\", \"object\", \"uuid\", \"datetime\", or regex:<pattern>. Avoid camelCase tokens like nonEmpty/notEmpty. Do not assert full message strings or session-specific values (token/id/timestamp); use existence/type/regex instead.\n" +
        "15. AUTH MODE (MACHINE-READABLE): Use executionHints.authMode with one of: none|optional|required.\n" +
        "   - Unauthorized/Missing Token tests: executionHints.authMode='none', request.headers must NOT contain Authorization.\n" +
        "   - Invalid token tests: executionHints.authMode='required' and set Authorization to an invalid literal.\n" +
        "   - Auth-required happy-path tests: executionHints.authMode='required', request.headers.Authorization='Bearer {{authToken}}', executionHints.consumes includes authToken, and executionHints.dependsOn references the login/token producer scenario key.\n" +
        "   - Legacy compatibility: you MAY also set X-Test-Auth-Mode header, but executionHints.authMode is the source of truth.\n" +
        "15b. CREDENTIAL REWRITE CONTROL: For scenarios that must preserve exact email/password from prompt intent, set executionHints.credentialPolicy and executionHints.lockedFields.\n" +
        "   - credentialPolicy values: preserve|rewrite_email|rewrite_password|rewrite_both.\n" +
        "   - lockedFields example: [\"request.body.email\",\"request.body.password\"].\n" +
        "16. For HappyPath, propose 1-3 bodyContains substrings and 1-2 JSONPath assertions on critical success fields when the response contract or SRS makes them inferable. Prefer contract-backed field names; use \"*\" only to assert existence.\n" +
        "17. For Boundary/Negative, propose 1-2 bodyContains substrings and 1 JSONPath assertion on the error payload when inferable. Prefer keywords and fields grounded in SRS constraints or errorResponses schema; avoid invented fields.\n" +
        "17b. If errorResponses[statusCode].schemaJson is provided, derive candidate bodyContains/jsonPathChecks from that schema instead of free-form guessing.\n" +
        "18. SRS-CONSTRAINT-DRIVEN: When srsContext.requirements[n].testableConstraints is non-empty, generate at least 1 scenario per meaningful constraint item. Rules:\n" +
        "   - The scenario's endpointId MUST be requirements[n].endpointId (if not null).\n" +
        "   - coveredRequirementCodes MUST include requirements[n].code.\n" +
        "   - expectedStatus/bodyContains/jsonPathChecks should mirror the constraint's expectedStatus/expectedOutcome, field, operator, value, sourceText and wording as closely as possible.\n" +
        "   - Example: constraint='password >= 6 chars', field='password', operator='minLength', value=6, expectedStatus=400 -> Boundary test, body={password:'12345'}, expectedStatus=[400], bodyContains=['password','minimum'].\n" +
        "   - If no testableConstraints, ignore this rule.\n" +
        "19. SRS OVERRIDES SECURITY EXPECTATIONS: If SRS says POST/PUT/PATCH/DELETE or a mapped endpoint requires auth, generate the auth happy path with Authorization even when OpenAPI omits security. Also generate a missing-auth Negative using X-Test-Auth-Mode:none and the SRS-backed 401/403 expectation.\n";

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
        "      \"executionHints\": {\n" +
        "        \"authMode\": \"none|optional|required\",\n" +
        "        \"credentialPolicy\": \"preserve|rewrite_email|rewrite_password|rewrite_both\",\n" +
        "        \"lockedFields\": [\"request.body.email\", \"request.body.password\"],\n" +
        "        \"produces\": [\"authToken\", \"categoryId\"],\n" +
        "        \"consumes\": [\"authToken\", \"categoryId\"],\n" +
        "        \"dependsOn\": [\"producer-scenario-key\"],\n" +
        "        \"flowRequired\": true,\n" +
        "        \"abortIfDependencyFailed\": true\n" +
        "      },\n" +
        "      \"coveredRequirementCodes\": [\"REQ-001\"],\n" +
        "      \"variables\": [{\"variableName\":\"authToken\",\"jsonPath\":\"$.token\",\"extractFrom\":\"ResponseBody\"}]\n" +
        "    }\n" +
        "  ],\n" +
        "  \"model\": \"<model name>\",\n" +
        "  \"tokensUsed\": 0\n" +
        "}";

    private static readonly Regex EmailLiteralRegex = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex JavaScriptRepeatExpressionRegex = new(
        @"\.repeat\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RouteParameterSegmentRegex = new(
        @"^\{\{?[A-Za-z0-9_.-]+\}?\}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PlaceholderValueRegex = new(
        @"\{\{\s*(?<name>[A-Za-z0-9_.-]+)\s*\}\}",
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
    private readonly IEndpointRequirementMapper _requirementMapper;
    private readonly IExpectationResolver _expectationResolver;
    private readonly ILogger<LlmScenarioSuggester> _logger;
    private readonly ScenarioGenerationBudgetOptions _scenarioBudgetOptions;
    private readonly ScenarioBudgetResolver _scenarioBudgetResolver;

    public LlmScenarioSuggester(
        IObservationConfirmationPromptBuilder promptBuilder,
        IN8nIntegrationService n8nService,
        ILlmAssistantGatewayService llmGatewayService,
        ILlmSuggestionFeedbackContextService feedbackContextService,
        IEndpointRequirementMapper requirementMapper,
        IExpectationResolver expectationResolver,
        ILogger<LlmScenarioSuggester> logger,
        IOptions<ScenarioGenerationBudgetOptions> scenarioBudgetOptions = null)
    {
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _n8nService = n8nService ?? throw new ArgumentNullException(nameof(n8nService));
        _llmGatewayService = llmGatewayService ?? throw new ArgumentNullException(nameof(llmGatewayService));
        _feedbackContextService = feedbackContextService ?? throw new ArgumentNullException(nameof(feedbackContextService));
        _requirementMapper = requirementMapper ?? throw new ArgumentNullException(nameof(requirementMapper));
        _expectationResolver = expectationResolver ?? throw new ArgumentNullException(nameof(expectationResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scenarioBudgetOptions = ScenarioBudgetResolver.Normalize(scenarioBudgetOptions?.Value);
        _scenarioBudgetResolver = new ScenarioBudgetResolver(_scenarioBudgetOptions);
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
        var scenarioBudgets = BuildScenarioBudgets(context, orderedSequence, metadataMap);
        var endpointBatches = BuildEndpointBatches(orderedSequence, scenarioBudgets);

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
                prompts = _promptBuilder.BuildForSequence(promptContexts) ?? Array.Empty<ObservationConfirmationPrompt>();
            }

            var payload = BuildN8nPayload(
                context,
                batch,
                metadataMap,
                prompts,
                feedbackContext.EndpointFeedbackContexts,
                scenarioBudgets);
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
            IReadOnlyList<LlmSuggestedScenario> localFallbackScenarios = null;

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

                localFallbackScenarios = (await SuggestLocalDraftAsync(
                    CreateBatchContext(context, batch),
                    cancellationToken)).Scenarios;
            }

            stopwatch.Stop();
            var latencyMs = (int)stopwatch.ElapsedMilliseconds;

            await SaveInteractionAsync(context, payload, n8nResponse, latencyMs, cancellationToken);

            var batchScenarios = localFallbackScenarios
                ?? ParseRefinementResponse(CreateBatchContext(context, batch), n8nResponse).Scenarios;
            allScenarios.AddRange(batchScenarios);

            if (algorithmProfile.UseFeedbackLoopContext && localFallbackScenarios == null)
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

        var endpointContracts = BuildEndpointContracts(context, orderedSequence, metadataMap);
        var filteredScenarios = ApplyScenarioBudgetPolicy(
            allScenarios,
            orderedSequence,
            metadataMap,
            scenarioBudgets,
            endpointContracts,
            context.SrsRequirements);
        if (filteredScenarios.Count != allScenarios.Count)
        {
            _logger.LogWarning(
                "Filtered LLM scenario suggestions before returning result. TestSuiteId={TestSuiteId}, RawCount={RawCount}, KeptCount={KeptCount}",
                context.TestSuiteId,
                allScenarios.Count,
                filteredScenarios.Count);
        }

        return new LlmScenarioSuggestionResult
        {
            Scenarios = filteredScenarios,
            LlmModel = modelUsed,
            TokensUsed = totalTokens,
            LatencyMs = totalLatencyMs,
            FromCache = false,
            UsedLocalFallback = usedLocalFallback,
        };
    }

    private static LlmScenarioSuggestionContext CreateBatchContext(
        LlmScenarioSuggestionContext context,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints)
    {
        return new LlmScenarioSuggestionContext
        {
            TestSuiteId = context.TestSuiteId,
            UserId = context.UserId,
            Suite = context.Suite,
            EndpointMetadata = context.EndpointMetadata,
            OrderedEndpoints = orderedEndpoints ?? Array.Empty<ApiOrderItemModel>(),
            SpecificationId = context.SpecificationId,
            EndpointParameterDetails = context.EndpointParameterDetails,
            AlgorithmProfile = context.AlgorithmProfile,
            BypassCache = context.BypassCache,
            SrsDocument = context.SrsDocument,
            SrsRequirements = context.SrsRequirements,
        };
    }

    public Task<LlmScenarioSuggestionResult> SuggestLocalDraftAsync(
        LlmScenarioSuggestionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context ??= new LlmScenarioSuggestionContext();

        var algorithmProfile = context.AlgorithmProfile ?? new GenerationAlgorithmProfile();
        var orderedSequence = algorithmProfile.UseDependencyAwareOrdering
            ? ApplyDependencyAwareOrdering(context.OrderedEndpoints)
            : context.OrderedEndpoints?.ToList() ?? new List<ApiOrderItemModel>();

        if (orderedSequence.Count == 0)
        {
            return Task.FromResult(new LlmScenarioSuggestionResult
            {
                Scenarios = Array.Empty<LlmSuggestedScenario>(),
                LlmModel = "local-fallback",
                TokensUsed = 0,
                LatencyMs = 0,
                FromCache = false,
                UsedLocalFallback = true,
            });
        }

        var metadataMap = context.EndpointMetadata?.ToDictionary(e => e.EndpointId)
            ?? new Dictionary<Guid, ApiEndpointMetadataDto>();
        var scenarioBudgets = BuildScenarioBudgets(context, orderedSequence, metadataMap);
        var endpointContracts = BuildEndpointContracts(context, orderedSequence, metadataMap);
        var scenarios = new List<LlmSuggestedScenario>();

        foreach (var endpoint in orderedSequence)
        {
            metadataMap.TryGetValue(endpoint.EndpointId, out var metadata);
            endpointContracts.TryGetValue(endpoint.EndpointId, out var contract);

            var method = ResolveHttpMethod(endpoint, metadata);
            var hasErrorResponse = metadata?.Responses?.Any(r => r.StatusCode >= 400 && r.StatusCode <= 599) == true;
            var hasSrsErrorExpectation = HasSrsErrorExpectation(endpoint, metadata, context.SrsRequirements);
            var hasSuccessResponse = metadata?.Responses?.Any(r => r.StatusCode >= 200 && r.StatusCode <= 299) == true
                || metadata?.Responses == null;

            if ((string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase) && !hasSrsErrorExpectation) ||
                (!hasErrorResponse && !hasSrsErrorExpectation && !HasBoundarySurface(endpoint, metadata)))
            {
                continue;
            }

            var index = 1;
            if (hasSuccessResponse)
            {
                scenarios.Add(CreateFallbackScenario(
                    endpoint,
                    metadata,
                    contract,
                    TestType.HappyPath,
                    index++,
                    context.SrsRequirements));
            }

            if (HasBoundarySurface(endpoint, metadata))
            {
                scenarios.Add(CreateFallbackScenario(
                    endpoint,
                    metadata,
                    contract,
                    TestType.Boundary,
                    index++,
                    context.SrsRequirements));
            }

            if (hasErrorResponse || hasSrsErrorExpectation)
            {
                scenarios.Add(CreateFallbackScenario(
                    endpoint,
                    metadata,
                    contract,
                    TestType.Negative,
                    index++,
                    context.SrsRequirements));
            }
        }

        var orderedScenarios = ApplyScenarioBudgetPolicy(
            scenarios,
            orderedSequence,
            metadataMap,
            scenarioBudgets,
            endpointContracts,
            context.SrsRequirements);

        return Task.FromResult(new LlmScenarioSuggestionResult
        {
            Scenarios = orderedScenarios,
            LlmModel = "local-fallback",
            TokensUsed = 0,
            LatencyMs = 0,
            FromCache = false,
            UsedLocalFallback = true,
        });
    }

    public async Task<N8nBoundaryNegativePayload> BuildAsyncRefinementPayloadAsync(
        LlmScenarioSuggestionContext context,
        Guid refinementJobId,
        string callbackUrl,
        string callbackApiKey,
        CancellationToken cancellationToken = default)
    {
        context ??= new LlmScenarioSuggestionContext();

        var algorithmProfile = context.AlgorithmProfile ?? new GenerationAlgorithmProfile();
        var orderedSequence = algorithmProfile.UseDependencyAwareOrdering
            ? ApplyDependencyAwareOrdering(context.OrderedEndpoints)
            : context.OrderedEndpoints?.ToList() ?? new List<ApiOrderItemModel>();

        var metadataMap = context.EndpointMetadata?.ToDictionary(e => e.EndpointId)
            ?? new Dictionary<Guid, ApiEndpointMetadataDto>();
        var scenarioBudgets = BuildScenarioBudgets(context, orderedSequence, metadataMap);

        var feedbackContext = algorithmProfile.UseFeedbackLoopContext
            ? await BuildFeedbackContextSafeAsync(context, orderedSequence, cancellationToken)
            : LlmSuggestionFeedbackContextResult.Empty;

        IReadOnlyList<ObservationConfirmationPrompt> prompts = Array.Empty<ObservationConfirmationPrompt>();
        if (algorithmProfile.UseObservationConfirmationPrompting)
        {
            var orderedMetadata = orderedSequence
                .Where(oe => metadataMap.ContainsKey(oe.EndpointId))
                .Select(oe => metadataMap[oe.EndpointId])
                .ToList();

            var promptContexts = EndpointPromptContextMapper.Map(orderedMetadata, context.Suite);
            prompts = _promptBuilder.BuildForSequence(promptContexts);
        }

        var payload = BuildN8nPayload(
            context,
            orderedSequence,
            metadataMap,
            prompts,
            feedbackContext.EndpointFeedbackContexts,
            scenarioBudgets);

        payload.RefinementJobId = refinementJobId;
        payload.CallbackUrl = callbackUrl;
        payload.CallbackApiKey = callbackApiKey;

        return payload;
    }

    public LlmScenarioSuggestionResult ParseRefinementResponse(
        LlmScenarioSuggestionContext context,
        N8nBoundaryNegativeResponse response)
    {
        context ??= new LlmScenarioSuggestionContext();
        var preserveN8nAsIs = true;

        var algorithmProfile = context.AlgorithmProfile ?? new GenerationAlgorithmProfile();
        var orderedSequence = algorithmProfile.UseDependencyAwareOrdering
            ? ApplyDependencyAwareOrdering(context.OrderedEndpoints)
            : context.OrderedEndpoints?.ToList() ?? new List<ApiOrderItemModel>();

        var metadataMap = context.EndpointMetadata?.ToDictionary(e => e.EndpointId)
            ?? new Dictionary<Guid, ApiEndpointMetadataDto>();
        var endpointContracts = BuildEndpointContracts(context, orderedSequence, metadataMap);
        var orderItemMap = orderedSequence.ToDictionary(e => e.EndpointId);

        var parsedScenarios = ParseScenarios(response, context.SrsRequirements);
        if (parsedScenarios.Count == 0)
        {
            return new LlmScenarioSuggestionResult
            {
                Scenarios = Array.Empty<LlmSuggestedScenario>(),
                LlmModel = response?.Model,
                TokensUsed = response?.TokensUsed,
                FromCache = false,
                UsedLocalFallback = false,
            };
        }

        var repaired = new List<LlmSuggestedScenario>();
        var remappedEndpointCount = 0;
        var droppedRouteMismatchCount = 0;
        foreach (var parsedScenario in parsedScenarios)
        {
            var scenario = parsedScenario;
            var originalEndpointId = scenario.EndpointId;
            var orderItem = ResolveOrderItemForScenario(
                scenario,
                orderedSequence,
                orderItemMap,
                metadataMap,
                out var routeResolution);
            if (orderItem == null)
            {
                droppedRouteMismatchCount++;
                _logger.LogWarning(
                    "Dropping LLM refinement scenario with unresolved route. ScenarioName={ScenarioName}, EndpointId={EndpointId}, HttpMethod={HttpMethod}, Url={Url}, Reason={Reason}",
                    scenario.ScenarioName,
                    originalEndpointId,
                    scenario.SuggestedHttpMethod,
                    scenario.SuggestedUrl,
                    routeResolution);
                continue;
            }

            if (scenario.EndpointId != orderItem.EndpointId)
            {
                remappedEndpointCount++;
                _logger.LogWarning(
                    "Remapped LLM refinement scenario endpoint by route. ScenarioName={ScenarioName}, OriginalEndpointId={OriginalEndpointId}, RemappedEndpointId={RemappedEndpointId}, HttpMethod={HttpMethod}, Url={Url}",
                    scenario.ScenarioName,
                    originalEndpointId,
                    orderItem.EndpointId,
                    scenario.SuggestedHttpMethod,
                    scenario.SuggestedUrl);
                scenario.EndpointId = orderItem.EndpointId;
            }

            metadataMap.TryGetValue(scenario.EndpointId, out var metadata);
            endpointContracts.TryGetValue(scenario.EndpointId, out var contract);
            scenario = FilterScenarioRequirementCoverage(scenario, metadata, context.SrsRequirements);

            scenario.SuggestedHttpMethod ??= orderItem?.HttpMethod ?? metadata?.HttpMethod;
            scenario.SuggestedUrl ??= orderItem?.Path ?? metadata?.Path;

            if (contract != null)
            {
                scenario = ContractAwareRequestSynthesizer.RepairScenario(scenario, contract.RequestContext);
                if (!IsScenarioContractComplete(scenario, contract, out _))
                {
                    scenario = CreateFallbackScenario(
                        orderItem,
                        metadata,
                        contract,
                        scenario.SuggestedTestType,
                        1,
                        context.SrsRequirements);
                }
            }

            if (!preserveN8nAsIs)
            {
                var llmExpectation = new N8nTestCaseExpectation
                {
                    ExpectedStatus = scenario.GetEffectiveExpectedStatusCodes(),
                    BodyContains = scenario.SuggestedBodyContains ?? new List<string>(),
                    BodyNotContains = scenario.SuggestedBodyNotContains ?? new List<string>(),
                    JsonPathChecks = scenario.SuggestedJsonPathChecks ?? new Dictionary<string, string>(),
                    HeaderChecks = scenario.SuggestedHeaderChecks ?? new Dictionary<string, string>(),
                    ExpectationSource = scenario.ExpectationSource,
                    RequirementCode = scenario.RequirementCode,
                    PrimaryRequirementId = scenario.PrimaryRequirementId,
                };

                var resolved = _expectationResolver.Resolve(new GeneratedScenarioContext
                {
                    EndpointId = scenario.EndpointId,
                    TestType = scenario.SuggestedTestType,
                    HttpMethod = scenario.SuggestedHttpMethod ?? orderItem?.HttpMethod ?? metadata?.HttpMethod,
                    SwaggerResponses = metadata?.Responses ?? Array.Empty<ApiEndpointResponseDescriptorDto>(),
                    LlmExpectation = llmExpectation,
                    SrsRequirements = context.SrsRequirements ?? Array.Empty<SrsRequirement>(),
                    CoveredRequirementIds = scenario.CoveredRequirementIds ?? new List<Guid>(),
                    PreferredDefaultStatuses = scenario.GetEffectiveExpectedStatusCodes(),
                    SrsDocumentContent = context.SrsDocument?.ParsedMarkdown ?? context.SrsDocument?.RawContent,
                });

                if (resolved != null)
                {
                    scenario.ExpectedStatusCodes = resolved.ExpectedStatusCodes?.ToList() ?? scenario.ExpectedStatusCodes;
                    if (scenario.ExpectedStatusCodes?.Count > 0)
                    {
                        scenario.ExpectedStatusCode = scenario.ExpectedStatusCodes[0];
                    }

                    scenario.SuggestedBodyContains = resolved.BodyContains?.ToList() ?? scenario.SuggestedBodyContains;
                    scenario.SuggestedBodyNotContains = resolved.BodyNotContains?.ToList() ?? scenario.SuggestedBodyNotContains;
                    scenario.SuggestedJsonPathChecks = resolved.JsonPathChecks != null
                        ? new Dictionary<string, string>(resolved.JsonPathChecks, StringComparer.OrdinalIgnoreCase)
                        : scenario.SuggestedJsonPathChecks;
                    scenario.SuggestedHeaderChecks = resolved.HeaderChecks != null
                        ? new Dictionary<string, string>(resolved.HeaderChecks, StringComparer.OrdinalIgnoreCase)
                        : scenario.SuggestedHeaderChecks;
                    scenario.ExpectationSource = resolved.Source.ToString();
                    scenario.RequirementCode = resolved.RequirementCode ?? scenario.RequirementCode;
                    scenario.PrimaryRequirementId = resolved.PrimaryRequirementId ?? scenario.PrimaryRequirementId;
                    scenario.ExpectedProvenance = resolved.ExpectedProvenance ?? scenario.ExpectedProvenance;
                }
            }

            var request = new N8nTestCaseRequest
            {
                HttpMethod = scenario.SuggestedHttpMethod,
                Url = scenario.SuggestedUrl,
                BodyType = scenario.SuggestedBodyType,
                Body = scenario.SuggestedBody,
                Headers = scenario.SuggestedHeaders ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PathParams = scenario.SuggestedPathParams ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                QueryParams = scenario.SuggestedQueryParams ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            };

            if (!preserveN8nAsIs)
            {
                scenario.SuggestedBodyContains = NormalizeRegisterBodyContains(
                    scenario.SuggestedBodyContains,
                    request,
                    scenario.ScenarioName);
                scenario.ExpectedBehavior = scenario.SuggestedBodyContains?.FirstOrDefault() ?? scenario.ExpectedBehavior;

                if (metadata?.Responses != null)
                {
                    scenario = RepairAssertionsFromSchema(scenario, metadata.Responses);
                }
            }

            repaired.Add(scenario);
        }

        NormalizeRefinementFlow(repaired, orderedSequence, metadataMap);

        // For callback refinement flow, preserve callback cardinality:
        // do not deduplicate/drop after repair.
        var orderedScenarios = repaired;

        _logger.LogInformation(
            "Parsed refinement scenarios. InputCount={InputCount}, RepairedCount={RepairedCount}, FinalCount={FinalCount}, RemappedEndpointCount={RemappedEndpointCount}, DroppedRouteMismatchCount={DroppedRouteMismatchCount}, BudgetCapApplied={BudgetCapApplied}",
            parsedScenarios.Count,
            repaired.Count,
            orderedScenarios.Count,
            remappedEndpointCount,
            droppedRouteMismatchCount,
            false);

        return new LlmScenarioSuggestionResult
        {
            Scenarios = orderedScenarios,
            LlmModel = response?.Model,
            TokensUsed = response?.TokensUsed,
            FromCache = false,
            UsedLocalFallback = false,
        };
    }

    private static ApiOrderItemModel ResolveOrderItemForScenario(
        LlmSuggestedScenario scenario,
        IReadOnlyList<ApiOrderItemModel> orderedSequence,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemMap,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap,
        out string reason)
    {
        reason = null;
        if (scenario == null)
        {
            reason = "Scenario is null.";
            return null;
        }

        orderItemMap.TryGetValue(scenario.EndpointId, out var currentOrderItem);
        metadataMap.TryGetValue(scenario.EndpointId, out var currentMetadata);

        var hasRoute = !string.IsNullOrWhiteSpace(scenario.SuggestedHttpMethod)
            && !string.IsNullOrWhiteSpace(scenario.SuggestedUrl);
        if (currentOrderItem != null && !hasRoute)
        {
            return currentOrderItem;
        }

        if (currentOrderItem != null &&
            RouteMatches(
                currentOrderItem.HttpMethod ?? currentMetadata?.HttpMethod,
                currentOrderItem.Path ?? currentMetadata?.Path,
                scenario.SuggestedHttpMethod,
                scenario.SuggestedUrl))
        {
            return currentOrderItem;
        }

        var matchingByRoute = orderedSequence
            .Where(item =>
            {
                metadataMap.TryGetValue(item.EndpointId, out var metadata);
                return RouteMatches(
                    item.HttpMethod ?? metadata?.HttpMethod,
                    item.Path ?? metadata?.Path,
                    scenario.SuggestedHttpMethod,
                    scenario.SuggestedUrl);
            })
            .ToList();

        if (matchingByRoute.Count == 1)
        {
            return matchingByRoute[0];
        }

        reason = matchingByRoute.Count == 0
            ? "No approved endpoint route matches the scenario request."
            : $"Scenario request route is ambiguous across {matchingByRoute.Count} approved endpoints.";
        return null;
    }

    private static bool RouteMatches(
        string approvedMethod,
        string approvedPath,
        string suggestedMethod,
        string suggestedUrl)
    {
        if (string.IsNullOrWhiteSpace(approvedMethod) ||
            string.IsNullOrWhiteSpace(approvedPath) ||
            string.IsNullOrWhiteSpace(suggestedMethod) ||
            string.IsNullOrWhiteSpace(suggestedUrl))
        {
            return false;
        }

        if (!string.Equals(approvedMethod.Trim(), suggestedMethod.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var approvedSegments = NormalizeRouteSegments(approvedPath);
        var suggestedSegments = NormalizeRouteSegments(suggestedUrl);
        if (approvedSegments.Count != suggestedSegments.Count)
        {
            return false;
        }

        for (var i = 0; i < approvedSegments.Count; i++)
        {
            var approvedSegment = approvedSegments[i];
            var suggestedSegment = suggestedSegments[i];
            if (IsRouteParameterSegment(approvedSegment))
            {
                if (string.IsNullOrWhiteSpace(suggestedSegment))
                {
                    return false;
                }

                continue;
            }

            if (IsRouteParameterSegment(suggestedSegment))
            {
                continue;
            }

            if (!string.Equals(approvedSegment, suggestedSegment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> NormalizeRouteSegments(string urlOrPath)
    {
        var path = NormalizeRoutePath(urlOrPath);
        return path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => Uri.UnescapeDataString(segment.Trim()))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();
    }

    private static string NormalizeRoutePath(string urlOrPath)
    {
        if (string.IsNullOrWhiteSpace(urlOrPath))
        {
            return string.Empty;
        }

        var trimmed = urlOrPath.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            trimmed = uri.AbsolutePath;
        }

        var queryIndex = trimmed.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
        {
            trimmed = trimmed[..queryIndex];
        }

        return trimmed.Trim();
    }

    private static bool IsRouteParameterSegment(string segment)
        => !string.IsNullOrWhiteSpace(segment) &&
           RouteParameterSegmentRegex.IsMatch(segment.Trim());

    private static void NormalizeRefinementFlow(
        IReadOnlyList<LlmSuggestedScenario> scenarios,
        IReadOnlyList<ApiOrderItemModel> orderedSequence,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        if (scenarios == null || scenarios.Count == 0)
        {
            return;
        }

        var orderItemMap = orderedSequence?
            .GroupBy(x => x.EndpointId)
            .ToDictionary(g => g.Key, g => g.First())
            ?? new Dictionary<Guid, ApiOrderItemModel>();

        var indexedScenarios = scenarios.Select((scenario, index) => (scenario, index)).ToList();
        var registerProducer = indexedScenarios
            .Where(x => IsSuccessfulHappyPath(x.scenario) && IsRegisterLikeScenario(x.scenario, orderItemMap, metadataMap))
            .OrderBy(x => x.index)
            .Select(x => x.scenario)
            .FirstOrDefault();
        if (registerProducer != null)
        {
            EnsureScenarioProduces(registerProducer, "email", "password");
        }

        foreach (var scenario in scenarios)
        {
            NormalizeLoginCredentials(scenario, registerProducer, orderItemMap, metadataMap);
        }

        var authProducer = SelectCanonicalAuthProducer(indexedScenarios, registerProducer, orderItemMap, metadataMap);
        if (authProducer != null)
        {
            EnsureScenarioProduces(authProducer, "authToken");
            EnsureAuthTokenVariable(authProducer);
        }

        var authProducerKeys = BuildDependencyKeySet(indexedScenarios
            .Where(x => IsSuccessfulHappyPath(x.scenario) && IsLoginLikeScenario(x.scenario, orderItemMap, metadataMap))
            .Select(x => x.scenario));

        foreach (var scenario in scenarios)
        {
            NormalizeRequiredAuth(scenario, authProducer, authProducerKeys);
        }

        foreach (var scenario in scenarios)
        {
            scenario.Tags = MergeFlowDependencyTags(scenario.Tags, scenario);
        }
    }

    private static bool IsSuccessfulHappyPath(LlmSuggestedScenario scenario)
        => scenario?.SuggestedTestType == TestType.HappyPath &&
           scenario.GetEffectiveExpectedStatusCodes().Any(status => status is >= 200 and <= 299);

    private static bool IsRegisterLikeScenario(
        LlmSuggestedScenario scenario,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemMap,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        if (scenario == null)
        {
            return false;
        }

        orderItemMap.TryGetValue(scenario.EndpointId, out var orderItem);
        metadataMap.TryGetValue(scenario.EndpointId, out var metadata);
        return IsRegisterLikeEndpoint(orderItem, metadata) ||
               IsRegisterLikeRequest(scenario.SuggestedHttpMethod, scenario.SuggestedUrl, scenario.ScenarioName);
    }

    private static bool IsLoginLikeScenario(
        LlmSuggestedScenario scenario,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemMap,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        if (scenario == null)
        {
            return false;
        }

        orderItemMap.TryGetValue(scenario.EndpointId, out var orderItem);
        metadataMap.TryGetValue(scenario.EndpointId, out var metadata);
        return IsLoginLikeEndpoint(orderItem, metadata) ||
               IsLoginLikeRequest(scenario.SuggestedHttpMethod, scenario.SuggestedUrl, scenario.ScenarioName);
    }

    private static void NormalizeLoginCredentials(
        LlmSuggestedScenario scenario,
        LlmSuggestedScenario registerProducer,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemMap,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        if (scenario == null || registerProducer == null ||
            !IsSuccessfulHappyPath(scenario) ||
            !IsLoginLikeScenario(scenario, orderItemMap, metadataMap) ||
            string.IsNullOrWhiteSpace(scenario.SuggestedBody))
        {
            return;
        }

        try
        {
            var body = JsonNode.Parse(scenario.SuggestedBody) as JsonObject;
            if (body == null)
            {
                return;
            }

            var changed = false;
            var rewriteEmail = ShouldRewriteCredential(scenario, body, "email");
            var rewritePassword = ShouldRewriteCredential(scenario, body, "password");
            if (rewriteEmail && ShouldRewriteCredentialPair(scenario, body, "password"))
            {
                rewritePassword = true;
            }

            if (rewriteEmail)
            {
                body["email"] = "{{email}}";
                changed = true;
            }

            if (rewritePassword)
            {
                body["password"] = "{{password}}";
                changed = true;
            }

            if (changed)
            {
                scenario.SuggestedBody = body.ToJsonString(JsonOpts);
                scenario.SuggestedBodyType = string.IsNullOrWhiteSpace(scenario.SuggestedBodyType)
                    ? "JSON"
                    : scenario.SuggestedBodyType;
            }

            if (!changed && !UsesReusableCredential(body, "email") && !UsesReusableCredential(body, "password"))
            {
                return;
            }

            AddScenarioDependency(scenario, registerProducer);
            AddUnique(scenario.Consumes, "email", "password");
            EnsureScenarioProduces(registerProducer, "email", "password");
        }
        catch
        {
            return;
        }
    }

    private static bool ShouldRewriteCredential(
        LlmSuggestedScenario scenario,
        JsonObject body,
        string propertyName)
    {
        if (IsCredentialFieldLocked(scenario, propertyName) ||
            !TryGetStringProperty(body, propertyName, out var raw) ||
            string.IsNullOrWhiteSpace(raw) ||
            IsReusableCredentialPlaceholder(raw, propertyName))
        {
            return false;
        }

        var policy = NormalizeCredentialPolicy(scenario?.CredentialPolicy);
        if (ShouldPolicyRewrite(policy, propertyName))
        {
            return true;
        }

        if (string.Equals(policy, "preserve", StringComparison.OrdinalIgnoreCase) &&
            !LooksSyntheticCredential(raw, propertyName))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldRewriteCredentialPair(
        LlmSuggestedScenario scenario,
        JsonObject body,
        string propertyName)
    {
        return !IsCredentialFieldLocked(scenario, propertyName) &&
               TryGetStringProperty(body, propertyName, out var raw) &&
               !string.IsNullOrWhiteSpace(raw) &&
               !IsReusableCredentialPlaceholder(raw, propertyName);
    }

    private static void NormalizeRequiredAuth(
        LlmSuggestedScenario scenario,
        LlmSuggestedScenario authProducer,
        IReadOnlySet<string> authProducerKeys)
    {
        if (scenario == null ||
            !string.Equals(GetAuthMode(scenario), "required", StringComparison.OrdinalIgnoreCase) ||
            IsExplicitNegativeAuthCase(scenario))
        {
            return;
        }

        RemoveNonCanonicalDependencies(scenario, authProducerKeys, GetScenarioDependencyKey(authProducer));

        scenario.SuggestedHeaders ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var authVariableName = "authToken";
        if (scenario.SuggestedHeaders.TryGetValue("Authorization", out var existingAuthorization) &&
            TryGetPlaceholderName(existingAuthorization, out var placeholderName))
        {
            authVariableName = placeholderName;
        }
        else if (!scenario.SuggestedHeaders.ContainsKey("Authorization") ||
                 string.IsNullOrWhiteSpace(existingAuthorization))
        {
            scenario.SuggestedHeaders["Authorization"] = "Bearer {{authToken}}";
        }

        AddUnique(scenario.Consumes, authVariableName);
        if (authProducer != null && authProducer != scenario)
        {
            AddScenarioDependency(scenario, authProducer);
        }
    }

    private static LlmSuggestedScenario SelectCanonicalAuthProducer(
        IReadOnlyList<(LlmSuggestedScenario scenario, int index)> indexedScenarios,
        LlmSuggestedScenario registerProducer,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemMap,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        return indexedScenarios
            .Where(x => IsSuccessfulHappyPath(x.scenario) && IsLoginLikeScenario(x.scenario, orderItemMap, metadataMap))
            .OrderByDescending(x => ScoreAuthProducerCandidate(x.scenario, registerProducer))
            .ThenBy(x => x.index)
            .Select(x => x.scenario)
            .FirstOrDefault();
    }

    private static int ScoreAuthProducerCandidate(
        LlmSuggestedScenario scenario,
        LlmSuggestedScenario registerProducer)
    {
        if (scenario == null)
        {
            return int.MinValue;
        }

        var score = 0;
        if (UsesReusableLoginCredentials(scenario))
        {
            score += 80;
        }

        if (DependsOnScenario(scenario, registerProducer))
        {
            score += 20;
        }

        if (scenario.Consumes?.Any(IsEmailLikeVariableName) == true)
        {
            score += 8;
        }

        if (scenario.Consumes?.Any(IsPasswordLikeVariableName) == true)
        {
            score += 8;
        }

        if (scenario.Produces?.Any(IsTokenLikeVariableName) == true)
        {
            score += 10;
        }

        if (scenario.Variables?.Any(v => IsTokenLikeVariableName(v?.VariableName)) == true)
        {
            score += 10;
        }

        return score;
    }

    private static bool TryGetStringProperty(JsonObject body, string propertyName, out string value)
    {
        if (body != null &&
            !string.IsNullOrWhiteSpace(propertyName) &&
            body.TryGetPropertyValue(propertyName, out var node) &&
            node is JsonValue jsonValue &&
            jsonValue.TryGetValue<string>(out var raw))
        {
            value = raw;
            return true;
        }

        value = null;
        return false;
    }

    private static bool UsesReusableLoginCredentials(LlmSuggestedScenario scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario?.SuggestedBody))
        {
            return false;
        }

        try
        {
            var body = JsonNode.Parse(scenario.SuggestedBody) as JsonObject;
            return UsesReusableCredential(body, "email") &&
                   UsesReusableCredential(body, "password");
        }
        catch
        {
            return false;
        }
    }

    private static bool UsesReusableCredential(JsonObject body, string propertyName)
    {
        return TryGetStringProperty(body, propertyName, out var raw) &&
               IsReusableCredentialPlaceholder(raw, propertyName);
    }

    private static bool IsReusableCredentialPlaceholder(string value, string propertyName)
    {
        return TryGetExactPlaceholderName(value, out var name) &&
               (string.Equals(propertyName, "email", StringComparison.OrdinalIgnoreCase)
                   ? IsEmailLikeVariableName(name)
                   : IsPasswordLikeVariableName(name));
    }

    private static bool TryGetExactPlaceholderName(string value, out string name)
    {
        var trimmed = value?.Trim();
        var match = PlaceholderValueRegex.Match(trimmed ?? string.Empty);
        if (match.Success && string.Equals(match.Value, trimmed, StringComparison.Ordinal))
        {
            name = match.Groups["name"].Value;
            return !string.IsNullOrWhiteSpace(name);
        }

        name = null;
        return false;
    }

    private static bool ShouldPolicyRewrite(string policy, string propertyName)
    {
        return string.Equals(policy, "rewrite_both", StringComparison.OrdinalIgnoreCase) ||
               (string.Equals(policy, "rewrite_email", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propertyName, "email", StringComparison.OrdinalIgnoreCase)) ||
               (string.Equals(policy, "rewrite_password", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propertyName, "password", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksSyntheticCredential(string value, string propertyName)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               PlaceholderValueRegex.IsMatch(value) &&
               !IsReusableCredentialPlaceholder(value, propertyName);
    }

    private static bool IsCredentialFieldLocked(LlmSuggestedScenario scenario, string propertyName)
    {
        if (scenario?.LockedFields == null || string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return scenario.LockedFields.Any(field =>
            string.Equals(field?.Trim(), propertyName, StringComparison.OrdinalIgnoreCase) ||
            field?.Trim().EndsWith("." + propertyName, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool DependsOnScenario(LlmSuggestedScenario scenario, LlmSuggestedScenario dependency)
    {
        var dependencyKey = NormalizeDependencyKey(GetScenarioDependencyKey(dependency));
        if (string.IsNullOrWhiteSpace(dependencyKey) || scenario?.DependsOn == null)
        {
            return false;
        }

        return scenario.DependsOn
            .Select(NormalizeDependencyKey)
            .Any(x => string.Equals(x, dependencyKey, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlySet<string> BuildDependencyKeySet(IEnumerable<LlmSuggestedScenario> scenarios)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (scenarios == null)
        {
            return keys;
        }

        foreach (var scenario in scenarios)
        {
            var key = NormalizeDependencyKey(GetScenarioDependencyKey(scenario));
            if (!string.IsNullOrWhiteSpace(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private static void RemoveNonCanonicalDependencies(
        LlmSuggestedScenario scenario,
        IReadOnlySet<string> dependencyKeys,
        string canonicalKey)
    {
        if (scenario == null || dependencyKeys == null || dependencyKeys.Count == 0)
        {
            return;
        }

        var normalizedCanonicalKey = NormalizeDependencyKey(canonicalKey);
        if (string.IsNullOrWhiteSpace(normalizedCanonicalKey))
        {
            return;
        }

        if (scenario.DependsOn != null)
        {
            scenario.DependsOn = scenario.DependsOn
                .Where(dep =>
                {
                    var normalized = NormalizeDependencyKey(dep);
                    return string.IsNullOrWhiteSpace(normalized) ||
                           !dependencyKeys.Contains(normalized) ||
                           string.Equals(normalized, normalizedCanonicalKey, StringComparison.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (scenario.Tags != null)
        {
            scenario.Tags = scenario.Tags
                .Where(tag =>
                {
                    const string prefix = "flow-depends-on:";
                    if (string.IsNullOrWhiteSpace(tag) ||
                        !tag.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var normalized = NormalizeDependencyKey(tag.Trim()[prefix.Length..]);
                    return string.IsNullOrWhiteSpace(normalized) ||
                           !dependencyKeys.Contains(normalized) ||
                           string.Equals(normalized, normalizedCanonicalKey, StringComparison.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private static string NormalizeDependencyKey(string key)
        => NormalizeScenarioKeyFromName(key);

    private static bool IsEmailLikeVariableName(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains("email", StringComparison.OrdinalIgnoreCase);

    private static bool IsPasswordLikeVariableName(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static bool IsTokenLikeVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty)
            .ToLowerInvariant();

        return normalized.Contains("token", StringComparison.Ordinal)
            || normalized.Contains("jwt", StringComparison.Ordinal)
            || normalized.Contains("bearer", StringComparison.Ordinal)
            || normalized.Contains("apikey", StringComparison.Ordinal)
            || normalized.Contains("authorization", StringComparison.Ordinal)
            || normalized.Contains("auth", StringComparison.Ordinal);
    }

    private static bool IsExplicitNegativeAuthCase(LlmSuggestedScenario scenario)
    {
        var expectedAuthFailure = scenario.GetEffectiveExpectedStatusCodes()
            .Any(status => status is 401 or 403);
        if (!expectedAuthFailure)
        {
            return false;
        }

        var surface = $"{scenario.ScenarioName} {scenario.Description} {string.Join(" ", scenario.Tags ?? new List<string>())}";
        if (surface.Contains("missing auth", StringComparison.OrdinalIgnoreCase) ||
            surface.Contains("without auth", StringComparison.OrdinalIgnoreCase) ||
            surface.Contains("no auth", StringComparison.OrdinalIgnoreCase) ||
            surface.Contains("invalid token", StringComparison.OrdinalIgnoreCase) ||
            surface.Contains("malformed token", StringComparison.OrdinalIgnoreCase) ||
            surface.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return scenario.SuggestedHeaders?.TryGetValue("Authorization", out var authValue) == true &&
               !string.IsNullOrWhiteSpace(authValue) &&
               !PlaceholderValueRegex.IsMatch(authValue);
    }

    private static string GetAuthMode(LlmSuggestedScenario scenario)
    {
        foreach (var tag in scenario?.Tags ?? new List<string>())
        {
            if (!tag.StartsWith("auth-mode:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return tag["auth-mode:".Length..].Trim();
        }

        return string.Empty;
    }

    private static bool TryGetPlaceholderName(string value, out string name)
    {
        var match = PlaceholderValueRegex.Match(value ?? string.Empty);
        name = match.Success ? match.Groups["name"].Value : null;
        return match.Success && !string.IsNullOrWhiteSpace(name);
    }

    private static void AddScenarioDependency(
        LlmSuggestedScenario scenario,
        LlmSuggestedScenario dependency)
    {
        var key = GetScenarioDependencyKey(dependency);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        scenario.DependsOn ??= new List<string>();
        AddUnique(scenario.DependsOn, key);
    }

    private static string GetScenarioDependencyKey(LlmSuggestedScenario scenario)
        => !string.IsNullOrWhiteSpace(scenario?.ScenarioKey)
            ? scenario.ScenarioKey.Trim()
            : NormalizeScenarioKeyFromName(scenario?.ScenarioName);

    private static void EnsureScenarioProduces(LlmSuggestedScenario scenario, params string[] variables)
    {
        if (scenario == null)
        {
            return;
        }

        scenario.Produces ??= new List<string>();
        AddUnique(scenario.Produces, variables);
    }

    private static void EnsureAuthTokenVariable(LlmSuggestedScenario scenario)
    {
        scenario.Variables ??= new List<N8nTestCaseVariable>();
        if (scenario.Variables.Any(v => string.Equals(v.VariableName, "authToken", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        scenario.Variables.Add(new N8nTestCaseVariable
        {
            VariableName = "authToken",
            ExtractFrom = "ResponseBody",
            JsonPath = "$.data.token",
        });
    }

    private static void AddUnique(List<string> target, params string[] values)
    {
        if (target == null || values == null)
        {
            return;
        }

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                !target.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(value);
            }
        }
    }

    private LlmSuggestedScenario FilterScenarioRequirementCoverage(
        LlmSuggestedScenario scenario,
        ApiEndpointMetadataDto endpoint,
        IReadOnlyList<SrsRequirement> requirements)
    {
        if (scenario == null ||
            endpoint == null ||
            requirements == null ||
            requirements.Count == 0 ||
            scenario.CoveredRequirementIds == null ||
            scenario.CoveredRequirementIds.Count == 0)
        {
            return scenario;
        }

        var coverableIds = _requirementMapper
            .MapRequirementsToEndpoint(endpoint, requirements)
            .Where(x => x.IsCoverable)
            .Select(x => x.Requirement?.Id ?? Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToHashSet();

        scenario.CoveredRequirementIds = scenario.CoveredRequirementIds
            .Where(coverableIds.Contains)
            .Distinct()
            .ToList();

        if (scenario.PrimaryRequirementId.HasValue &&
            !scenario.CoveredRequirementIds.Contains(scenario.PrimaryRequirementId.Value))
        {
            scenario.PrimaryRequirementId = null;
            scenario.RequirementCode = null;
        }

        return scenario;
    }

    private IReadOnlyDictionary<Guid, ScenarioBudget> BuildScenarioBudgets(
        LlmScenarioSuggestionContext context,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap)
    {
        var result = new Dictionary<Guid, ScenarioBudget>();
        if (orderedEndpoints == null || orderedEndpoints.Count == 0)
        {
            return result;
        }

        foreach (var endpoint in orderedEndpoints)
        {
            ApiEndpointMetadataDto metadata = null;
            metadataMap?.TryGetValue(endpoint.EndpointId, out metadata);

            string businessContext = null;
            context?.Suite?.EndpointBusinessContexts?.TryGetValue(endpoint.EndpointId, out businessContext);

            var coverableRequirementCount = 0;
            var dependencyRequirementCount = 0;
            if (metadata != null && context?.SrsRequirements?.Count > 0)
            {
                var matches = _requirementMapper.MapRequirementsToEndpoint(metadata, context.SrsRequirements);
                coverableRequirementCount = matches.Count(x => x.IsCoverable);
                dependencyRequirementCount = matches.Count(x =>
                    x.Relevance == RequirementRelevance.Dependency &&
                    x.Confidence != RequirementMatchConfidence.Low);
            }

            result[endpoint.EndpointId] = _scenarioBudgetResolver.Resolve(
                endpoint,
                metadata,
                businessContext,
                coverableRequirementCount,
                dependencyRequirementCount);
        }

        return result;
    }

    private List<List<ApiOrderItemModel>> BuildEndpointBatches(
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ScenarioBudget> scenarioBudgets)
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
            var target = ResolveBudgetEstimate(endpoint, scenarioBudgets);

            if (current.Count > 0 && currentTarget + target > _scenarioBudgetOptions.MaxScenarioBudgetPerBatch)
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

    private static int ResolveBudgetEstimate(
        ApiOrderItemModel endpoint,
        IReadOnlyDictionary<Guid, ScenarioBudget> scenarioBudgets)
    {
        if (endpoint != null &&
            scenarioBudgets != null &&
            scenarioBudgets.TryGetValue(endpoint.EndpointId, out var budget) &&
            budget != null)
        {
            return Math.Max(1, Math.Min(
                budget.HardLimit <= 0 ? budget.Target : budget.HardLimit,
                budget.Target > 0 ? budget.Target : budget.SoftLimit));
        }

        return 1;
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
        IReadOnlyDictionary<Guid, ScenarioBudget> scenarioBudgets)
    {
        var endpointPayloads = new List<N8nBoundaryEndpointPayload>();

        for (int i = 0; i < orderedEndpoints.Count; i++)
        {
            var orderItem = orderedEndpoints[i];
            metadataMap.TryGetValue(orderItem.EndpointId, out var metadata);
            ScenarioBudget scenarioBudget = null;
            scenarioBudgets?.TryGetValue(orderItem.EndpointId, out scenarioBudget);

            context.Suite.EndpointBusinessContexts.TryGetValue(orderItem.EndpointId, out var businessContext);
            var requirementMatches = metadata != null && context.SrsRequirements?.Count > 0
                ? _requirementMapper.MapRequirementsToEndpoint(metadata, context.SrsRequirements)
                : Array.Empty<RequirementMatch>();
            var endpointRequirements = requirementMatches
                .Where(x => x.IsCoverable)
                .Select(x => x.Requirement)
                .Where(x => x != null)
                .DistinctBy(x => x.Id)
                .Take(20)
                .Select(ToN8nRequirementBrief)
                .ToList();
            var requirementMatchBriefs = requirementMatches
                .Where(x =>
                    x.IsCoverable ||
                    (x.Relevance == RequirementRelevance.Dependency &&
                     x.Confidence != RequirementMatchConfidence.Low))
                .Take(20)
                .Select(ToN8nRequirementMatchBrief)
                .ToList();

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
                prompt,
                scenarioBudget);

            endpointPayloads.Add(new N8nBoundaryEndpointPayload
            {
                EndpointId = orderItem.EndpointId,
                HttpMethod = orderItem.HttpMethod,
                Path = orderItem.Path,
                OperationId = metadata?.OperationId,
                OrderIndex = orderItem.OrderIndex,
                BusinessContext = TruncateForPayload(businessContext, MaxBusinessContextLength),
                FeedbackContext = endpointFeedbackContexts != null && endpointFeedbackContexts.TryGetValue(orderItem.EndpointId, out var feedbackContext)
                    ? TruncateForPayload(feedbackContext, MaxFeedbackContextLength)
                    : string.Empty,
                ScenarioBudget = scenarioBudget,
                Prompt = promptPayload,
                ParameterSchemaPayloads = CompactSchemaPayloads(metadata?.ParameterSchemaPayloads),
                ResponseSchemaPayloads = CompactSchemaPayloads(metadata?.ResponseSchemaPayloads),
                ParameterDetails = BuildParameterDetails(context, orderItem.EndpointId),
                SrsRequirements = endpointRequirements,
                RequirementMatches = requirementMatchBriefs,
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
            PromptConfig = BuildSuggestionPromptConfig(context, prompts, orderedEndpoints, metadataMap, scenarioBudgets),
            Endpoints = endpointPayloads,
        };
    }

    private static N8nSrsContext BuildSrsContext(LlmScenarioSuggestionContext context)
    {
        if (context.SrsDocument == null)
            return null;

        var requirements = (context.SrsRequirements ?? Array.Empty<SrsRequirement>())
            .Where(IsGlobalRequirement)
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.RequirementCode)
            .Take(30)
            .Select(ToN8nRequirementBrief)
            .ToList();

        return new N8nSrsContext
        {
            DocumentTitle = context.SrsDocument.Title,
            Content = null,
            Requirements = requirements,
        };
    }

    private static bool IsGlobalRequirement(SrsRequirement requirement)
    {
        return requirement != null &&
               !requirement.EndpointId.HasValue &&
               string.IsNullOrWhiteSpace(requirement.MappedEndpointPath);
    }

    private static N8nSrsRequirementBrief ToN8nRequirementBrief(SrsRequirement requirement)
    {
        return new N8nSrsRequirementBrief
        {
            Code = requirement.RequirementCode,
            Title = requirement.Title,
            Description = TruncateForPayload(requirement.Description, 400),
            EndpointId = requirement.EndpointId,
            TestableConstraints = DeserializeTestableConstraints(
                requirement.RefinedConstraints ?? requirement.TestableConstraints),
        };
    }

    private static N8nRequirementMatchBrief ToN8nRequirementMatchBrief(RequirementMatch match)
    {
        return new N8nRequirementMatchBrief
        {
            Code = match.RequirementCode,
            Relevance = match.Relevance.ToString(),
            Confidence = match.Confidence.ToString(),
            MatchedSignals = match.MatchedSignals?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList() ?? new List<string>(),
        };
    }

    private static N8nPromptPayload BuildEndpointPromptPayload(
        ApiOrderItemModel orderItem,
        TestSuite suite,
        ApiEndpointMetadataDto metadata,
        string businessContext,
        ObservationConfirmationPrompt prompt,
        ScenarioBudget scenarioBudget)
    {
        var combinedPrompt = string.IsNullOrWhiteSpace(prompt?.CombinedPrompt)
            ? BuildFallbackCombinedPrompt(orderItem, suite, metadata, businessContext, scenarioBudget)
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
        string businessContext,
        ScenarioBudget scenarioBudget)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Endpoint Context (Fallback)");
        sb.AppendLine($"Method: {orderItem?.HttpMethod ?? metadata?.HttpMethod ?? "GET"}");
        sb.AppendLine($"Path: {orderItem?.Path ?? metadata?.Path ?? "/"}");
        sb.AppendLine($"OperationId: {metadata?.OperationId ?? "N/A"}");
        sb.AppendLine();
        sb.AppendLine("Generate boundary and negative scenarios for this endpoint only.");
        if (scenarioBudget != null)
        {
            sb.AppendLine($"Scenario budget: target={scenarioBudget.Target}, softLimit={scenarioBudget.SoftLimit}, hardLimit={scenarioBudget.HardLimit}. Reason: {scenarioBudget.Reason}");
        }

        sb.AppendLine("Use the scenario budget as the preferred limit; do not pad weak or duplicate variants and do not exceed hardLimit.");

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
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap,
        IReadOnlyDictionary<Guid, ScenarioBudget> scenarioBudgets)
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
                BuildSuggestionTaskInstruction(context?.Suite, orderedEndpoints, metadataMap, scenarioBudgets, context?.SrsRequirements),
                MaxTaskInstructionLength),
            Rules = TruncateForPayload(SuggestionRulesBlock, MaxRulesLength),
            ResponseFormat = TruncateForPayload(SuggestionResponseFormatBlock, MaxResponseFormatLength),
        };
    }

    private static string BuildSuggestionTaskInstruction(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap,
        IReadOnlyDictionary<Guid, ScenarioBudget> scenarioBudgets,
        IReadOnlyList<SrsRequirement> srsRequirements)
    {
        var suiteName = string.IsNullOrWhiteSpace(suite?.Name) ? "N/A" : suite.Name;
        var sb = new StringBuilder();
        sb.Append($"Generate happy-path, boundary, and negative test scenarios for this ordered REST API sequence (suite: {suiteName}).");
        sb.AppendLine();
        sb.Append("Use OpenAPI for method/path/request shape. Use mapped SRS requirements as the oracle for auth/security/business expectations when OpenAPI is incomplete or conflicts with SRS.");

        if (orderedEndpoints != null && orderedEndpoints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("=== SCENARIO BUDGET GUIDE ===");
            foreach (var endpoint in orderedEndpoints.OrderBy(x => x.OrderIndex))
            {
                ApiEndpointMetadataDto metadata = null;
                metadataMap?.TryGetValue(endpoint.EndpointId, out metadata);
                ScenarioBudget budget = null;
                scenarioBudgets?.TryGetValue(endpoint.EndpointId, out budget);
                var hasBoundarySurface = HasBoundarySurface(endpoint, metadata);
                var hasSrsErrorExpectation = HasSrsErrorExpectation(endpoint, metadata, srsRequirements);
                var expectedTypes = hasBoundarySurface
                    ? "HappyPath, Boundary, Negative"
                    : hasSrsErrorExpectation
                        ? "HappyPath, Negative"
                    : "HappyPath, Negative";

                if (budget == null)
                {
                    sb.AppendLine(
                        $"- [{endpoint.OrderIndex}] {endpoint.HttpMethod} {endpoint.Path}: prioritize {expectedTypes}; do not pad duplicates.");
                    continue;
                }

                sb.AppendLine(
                    $"- [{endpoint.OrderIndex}] {endpoint.HttpMethod} {endpoint.Path}: target={budget.Target}, softLimit={budget.SoftLimit}, hardLimit={budget.HardLimit}; reason={budget.Reason}; prioritize {expectedTypes}; do not pad duplicates.");
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
                PrimaryRequirementId = s.Expectation?.PrimaryRequirementId
                    ?? (!string.IsNullOrWhiteSpace(s.Expectation?.RequirementCode) &&
                        codeToId.TryGetValue(s.Expectation.RequirementCode.Trim(), out var requirementId)
                            ? requirementId
                            : null),
                CredentialPolicy = s.ExecutionHints?.CredentialPolicy ?? s.CredentialPolicy,
                LockedFields = s.ExecutionHints?.LockedFields?.Count > 0
                    ? s.ExecutionHints.LockedFields
                    : s.LockedFields ?? new List<string>(),
                ScenarioKey = !string.IsNullOrWhiteSpace(s.ScenarioKey)
                    ? NormalizeScenarioKeyFromName(s.ScenarioKey)
                    : (string.IsNullOrWhiteSpace(s.ScenarioName)
                        ? null
                        : NormalizeScenarioKeyFromName(s.ScenarioName)),
                FlowRequired = s.ExecutionHints?.FlowRequired,
                FlowId = s.ExecutionHints?.FlowId,
                DependsOn = s.ExecutionHints?.DependsOn?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    ?? new List<string>(),
                Produces = s.ExecutionHints?.Produces?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    ?? new List<string>(),
                Consumes = s.ExecutionHints?.Consumes?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    ?? new List<string>(),
                AbortIfDependencyFailed = s.ExecutionHints?.AbortIfDependencyFailed,
            };

            parsedScenario.Tags = MergeCredentialControlTags(
                parsedScenario.Tags,
                parsedScenario.CredentialPolicy,
                parsedScenario.LockedFields);
            parsedScenario.Tags = MergeAuthModeTag(
                parsedScenario.Tags,
                s.ExecutionHints?.AuthMode ?? s.AuthMode);
            parsedScenario.Tags = MergeFlowDependencyTags(parsedScenario.Tags, parsedScenario);

            if (parsedScenario.PrimaryRequirementId.HasValue &&
                !parsedScenario.CoveredRequirementIds.Contains(parsedScenario.PrimaryRequirementId.Value))
            {
                parsedScenario.CoveredRequirementIds.Add(parsedScenario.PrimaryRequirementId.Value);
            }

            scenarios.Add(parsedScenario);
        }

        return scenarios;
    }

    private static List<string> MergeCredentialControlTags(
        List<string> tags,
        string credentialPolicy,
        IReadOnlyCollection<string> lockedFields)
    {
        var merged = new List<string>(tags ?? new List<string>());
        var seen = new HashSet<string>(merged, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(credentialPolicy))
        {
            var normalizedPolicy = NormalizeCredentialPolicy(credentialPolicy);
            if (!string.IsNullOrWhiteSpace(normalizedPolicy))
            {
                var policyTag = $"cred-policy:{normalizedPolicy}";
                if (seen.Add(policyTag))
                {
                    merged.Add(policyTag);
                }
            }
        }

        if (lockedFields != null)
        {
            foreach (var field in lockedFields)
            {
                if (string.IsNullOrWhiteSpace(field))
                {
                    continue;
                }

                var normalizedField = field.Trim().ToLowerInvariant();
                var lockTag = $"cred-lock:{normalizedField}";
                if (seen.Add(lockTag))
                {
                    merged.Add(lockTag);
                }
            }
        }

        return merged;
    }

    private static List<string> MergeAuthModeTag(
        List<string> tags,
        string authMode)
    {
        var merged = new List<string>(tags ?? new List<string>());
        var normalized = NormalizeAuthMode(authMode);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return merged;
        }

        var authTag = $"auth-mode:{normalized}";
        if (!merged.Contains(authTag, StringComparer.OrdinalIgnoreCase))
        {
            merged.Add(authTag);
        }

        return merged;
    }

    private static List<string> MergeFlowDependencyTags(
        List<string> tags,
        LlmSuggestedScenario scenario)
    {
        var merged = new List<string>(tags ?? new List<string>());
        var seen = new HashSet<string>(merged, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(scenario?.ScenarioKey))
        {
            var scenarioKeyTag = $"flow-scenario-key:{scenario.ScenarioKey.Trim()}";
            if (seen.Add(scenarioKeyTag))
            {
                merged.Add(scenarioKeyTag);
            }
        }

        if (!string.IsNullOrWhiteSpace(scenario?.FlowId))
        {
            var flowIdTag = $"flow-id:{scenario.FlowId.Trim()}";
            if (seen.Add(flowIdTag))
            {
                merged.Add(flowIdTag);
            }
        }

        if (scenario?.FlowRequired.HasValue == true)
        {
            var flowRequiredTag = $"flow-required:{(scenario.FlowRequired.Value ? "true" : "false")}";
            if (seen.Add(flowRequiredTag))
            {
                merged.Add(flowRequiredTag);
            }
        }

        if (scenario?.AbortIfDependencyFailed.HasValue == true)
        {
            var abortTag = $"flow-abort-on-dep-fail:{(scenario.AbortIfDependencyFailed.Value ? "true" : "false")}";
            if (seen.Add(abortTag))
            {
                merged.Add(abortTag);
            }
        }

        foreach (var dependsOnKey in scenario?.DependsOn ?? new List<string>())
        {
            var depTag = $"flow-depends-on:{dependsOnKey}";
            if (seen.Add(depTag))
            {
                merged.Add(depTag);
            }
        }

        foreach (var produces in scenario?.Produces ?? new List<string>())
        {
            var producesTag = $"flow-produces:{produces}";
            if (seen.Add(producesTag))
            {
                merged.Add(producesTag);
            }
        }

        foreach (var consumes in scenario?.Consumes ?? new List<string>())
        {
            var consumesTag = $"flow-consumes:{consumes}";
            if (seen.Add(consumesTag))
            {
                merged.Add(consumesTag);
            }
        }

        return merged;
    }

    private static string NormalizeScenarioKeyFromName(string scenarioName)
    {
        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            return null;
        }

        var chars = scenarioName.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var normalized = new string(chars);

        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Trim('-');
    }

    private static string NormalizeCredentialPolicy(string policy)
    {
        var normalized = policy?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "preserve" => "preserve",
            "rewrite_email" => "rewrite_email",
            "rewrite_password" => "rewrite_password",
            "rewrite_both" => "rewrite_both",
            _ => string.Empty,
        };
    }

    private static string NormalizeAuthMode(string authMode)
    {
        var normalized = authMode?.Trim().ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        return normalized switch
        {
            "none" or "noauth" or "disableauth" => "none",
            "optional" => "optional",
            "required" or "default" => "required",
            _ => string.Empty,
        };
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

    private static bool IsLoginLikeRequest(string httpMethod, string url, string name)
    {
        var signature = $"{httpMethod} {url} {name}";
        return signature.Contains("/login", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("/signin", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("/sign-in", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("authenticate", StringComparison.OrdinalIgnoreCase)
            || signature.Contains("/token", StringComparison.OrdinalIgnoreCase);
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

    private IReadOnlyList<LlmSuggestedScenario> ApplyScenarioBudgetPolicy(
        IReadOnlyList<LlmSuggestedScenario> rawScenarios,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataMap,
        IReadOnlyDictionary<Guid, ScenarioBudget> scenarioBudgets,
        IReadOnlyDictionary<Guid, EndpointRequestContract> endpointContracts,
        IReadOnlyList<SrsRequirement> srsRequirements,
        bool enforceBudgetCap = true)
    {
        if (orderedEndpoints == null || orderedEndpoints.Count == 0)
        {
            return rawScenarios ?? Array.Empty<LlmSuggestedScenario>();
        }

        // Order the LLM-returned scenarios by endpoint order, then remove duplicates and low-quality padded cases.
        var byEndpoint = (rawScenarios ?? Array.Empty<LlmSuggestedScenario>())
            .GroupBy(x => x.EndpointId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<LlmSuggestedScenario>();
        var seenFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in orderedEndpoints.OrderBy(x => x.OrderIndex))
        {
            if (!byEndpoint.TryGetValue(endpoint.EndpointId, out var list) || list.Count == 0)
            {
                continue;
            }

            ApiEndpointMetadataDto metadata = null;
            metadataMap?.TryGetValue(endpoint.EndpointId, out metadata);
            ScenarioBudget budget = null;
            scenarioBudgets?.TryGetValue(endpoint.EndpointId, out budget);
            var candidates = new List<LlmSuggestedScenario>();
            foreach (var scenario in list)
            {
                if (scenario == null)
                {
                    continue;
                }

                RepairScenarioRequestContract(scenario, endpoint, metadata);
                if (!IsScenarioRequestUsable(scenario))
                {
                    continue;
                }

                if (ContainsInvalidJsonBodyExpression(scenario.SuggestedBody, scenario.SuggestedBodyType))
                {
                    continue;
                }

                var fingerprint = BuildScenarioFingerprint(scenario);
                if (!seenFingerprints.Add(fingerprint))
                {
                    continue;
                }

                candidates.Add(scenario);
            }

            if (!enforceBudgetCap)
            {
                result.AddRange(candidates);
                continue;
            }

            var cap = ResolveScenarioBudgetCap(budget);
            var selected = SelectHighValueScenarios(candidates, cap, metadata);
            if (candidates.Count > selected.Count)
            {
                _logger.LogInformation(
                    "Dropped {DroppedCount} LLM scenario suggestion(s) due to scenario budget. EndpointId={EndpointId}, Target={Target}, SoftLimit={SoftLimit}, HardLimit={HardLimit}, CandidateCount={CandidateCount}, KeptCount={KeptCount}",
                    candidates.Count - selected.Count,
                    endpoint.EndpointId,
                    budget?.Target,
                    budget?.SoftLimit,
                    budget?.HardLimit,
                    candidates.Count,
                    selected.Count);
            }

            result.AddRange(selected);
        }

        return result;
    }

    private static int ResolveScenarioBudgetCap(ScenarioBudget budget)
    {
        if (budget == null)
        {
            return int.MaxValue;
        }

        var preferred = budget.Target > 0 ? budget.Target : budget.SoftLimit;
        if (preferred <= 0)
        {
            preferred = budget.HardLimit;
        }

        if (budget.HardLimit > 0)
        {
            preferred = Math.Min(preferred, budget.HardLimit);
        }

        return preferred <= 0 ? int.MaxValue : preferred;
    }

    private static void RepairScenarioRequestContract(
        LlmSuggestedScenario scenario,
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata)
    {
        if (scenario == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(scenario.SuggestedHttpMethod))
        {
            scenario.SuggestedHttpMethod = endpoint?.HttpMethod ?? metadata?.HttpMethod;
        }

        if (string.IsNullOrWhiteSpace(scenario.SuggestedUrl))
        {
            scenario.SuggestedUrl = endpoint?.Path ?? metadata?.Path;
        }
    }

    private static bool IsScenarioRequestUsable(LlmSuggestedScenario scenario)
    {
        return !string.IsNullOrWhiteSpace(scenario?.SuggestedHttpMethod)
            && !string.IsNullOrWhiteSpace(scenario.SuggestedUrl);
    }

    private static bool ContainsInvalidJsonBodyExpression(string body, string bodyType)
    {
        return !string.IsNullOrWhiteSpace(body)
            && !IsRawBodyType(bodyType)
            && JavaScriptRepeatExpressionRegex.IsMatch(body);
    }

    private static bool IsRawBodyType(string bodyType)
    {
        return string.Equals(bodyType?.Trim(), "Raw", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<LlmSuggestedScenario> SelectHighValueScenarios(
        IReadOnlyList<LlmSuggestedScenario> candidates,
        int cap,
        ApiEndpointMetadataDto metadata)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return Array.Empty<LlmSuggestedScenario>();
        }

        if (cap <= 0 || candidates.Count <= cap)
        {
            return candidates.ToList();
        }

        var indexed = candidates
            .Select((scenario, index) => new { Scenario = scenario, Index = index })
            .ToList();
        var selectedIndexes = new HashSet<int>();
        var selected = new List<(LlmSuggestedScenario Scenario, int Index)>();

        var firstHappyPath = indexed.FirstOrDefault(x => x.Scenario.SuggestedTestType == TestType.HappyPath);
        if (firstHappyPath != null)
        {
            selected.Add((firstHappyPath.Scenario, firstHappyPath.Index));
            selectedIndexes.Add(firstHappyPath.Index);
        }

        foreach (var item in indexed
            .Where(x => !selectedIndexes.Contains(x.Index))
            .OrderBy(x => GetScenarioValueRank(x.Scenario, metadata))
            .ThenBy(x => GetPriorityRank(x.Scenario.Priority))
            .ThenBy(x => x.Index))
        {
            if (selected.Count >= cap)
            {
                break;
            }

            selected.Add((item.Scenario, item.Index));
            selectedIndexes.Add(item.Index);
        }

        return selected
            .OrderBy(x => x.Index)
            .Select(x => x.Scenario)
            .ToList();
    }

    private static int GetScenarioValueRank(LlmSuggestedScenario scenario, ApiEndpointMetadataDto metadata)
    {
        if (IsRequirementBackedScenario(scenario))
        {
            return 0;
        }

        if (IsAuthOrSecurityScenario(scenario))
        {
            return 1;
        }

        if (UsesDocumentedStatus(scenario, metadata))
        {
            return 2;
        }

        return scenario?.SuggestedTestType switch
        {
            TestType.Negative => 3,
            TestType.Boundary => 4,
            TestType.HappyPath => 5,
            TestType.Security => 6,
            TestType.Performance => 7,
            _ => 8,
        };
    }

    private static bool IsRequirementBackedScenario(LlmSuggestedScenario scenario)
    {
        return scenario?.PrimaryRequirementId.HasValue == true
            || scenario?.CoveredRequirementIds?.Any(x => x != Guid.Empty) == true
            || !string.IsNullOrWhiteSpace(scenario?.RequirementCode);
    }

    private static bool IsAuthOrSecurityScenario(LlmSuggestedScenario scenario)
    {
        if (scenario == null)
        {
            return false;
        }

        if (scenario.SuggestedTestType == TestType.Security)
        {
            return true;
        }

        if (scenario.GetEffectiveExpectedStatusCodes().Any(x => x is 401 or 403))
        {
            return true;
        }

        var text = string.Join(" ", new[]
        {
            scenario.ScenarioName,
            scenario.Description,
            string.Join(" ", scenario.Tags ?? new List<string>()),
        });

        return text.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || text.Contains("security", StringComparison.OrdinalIgnoreCase)
            || text.Contains("token", StringComparison.OrdinalIgnoreCase);
    }

    private static bool UsesDocumentedStatus(LlmSuggestedScenario scenario, ApiEndpointMetadataDto metadata)
    {
        var documentedStatuses = metadata?.Responses?
            .Select(x => x.StatusCode)
            .ToHashSet()
            ?? new HashSet<int>();

        return documentedStatuses.Count > 0
            && scenario?.GetEffectiveExpectedStatusCodes().Any(documentedStatuses.Contains) == true;
    }

    private static int GetPriorityRank(string priority)
    {
        return priority?.Trim().ToLowerInvariant() switch
        {
            "critical" => 0,
            "high" => 1,
            "medium" or "normal" => 2,
            "low" => 3,
            _ => 4,
        };
    }

    private static string BuildScenarioFingerprint(LlmSuggestedScenario scenario)
    {
        if (scenario == null)
        {
            return string.Empty;
        }

        return string.Join("|", new[]
        {
            scenario.EndpointId.ToString("N"),
            NormalizeFingerprintText(scenario.SuggestedHttpMethod).ToUpperInvariant(),
            NormalizeFingerprintText(scenario.SuggestedUrl),
            NormalizeFingerprintDictionary(scenario.SuggestedPathParams),
            NormalizeFingerprintDictionary(scenario.SuggestedQueryParams),
            NormalizeFingerprintDictionary(scenario.SuggestedHeaders),
            NormalizeFingerprintText(scenario.SuggestedBodyType).ToUpperInvariant(),
            NormalizeJsonOrTextForFingerprint(scenario.SuggestedBody),
            scenario.SuggestedTestType.ToString(),
            string.Join(",", scenario.GetEffectiveExpectedStatusCodes().OrderBy(x => x)),
            string.Join(",", (scenario.CoveredRequirementIds ?? new List<Guid>()).Where(x => x != Guid.Empty).OrderBy(x => x)),
        });
    }

    private static string NormalizeFingerprintDictionary(Dictionary<string, string> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("&", values
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{NormalizeFingerprintText(x.Key)}={NormalizeJsonOrTextForFingerprint(x.Value)}"));
    }

    private static string NormalizeJsonOrTextForFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return JsonSerializer.Serialize(doc.RootElement, JsonOpts);
        }
        catch
        {
            return trimmed;
        }
    }

    private static string NormalizeFingerprintText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string ResolveHttpMethod(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        return (endpoint?.HttpMethod ?? metadata?.HttpMethod ?? string.Empty).Trim().ToUpperInvariant();
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
            SuggestedHttpMethod = method,
            SuggestedUrl = path,
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
            CoveredRequirementIds = resolvedExpectation?.PrimaryRequirementId is Guid requirementId
                ? new List<Guid> { requirementId }
                : new List<Guid>(),
            ExpectationSource = (resolvedExpectation?.Source ?? Models.ExpectationSource.Default).ToString(),
            RequirementCode = resolvedExpectation?.RequirementCode,
            PrimaryRequirementId = resolvedExpectation?.PrimaryRequirementId,
            ExpectedProvenance = resolvedExpectation?.ExpectedProvenance,
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

            var requiresAuthFromSrs = HasSrsAuthRequirement(endpoint, metadata, context.SrsRequirements);
            var requestContext = new ContractAwareRequestContext
            {
                HttpMethod = endpoint.HttpMethod ?? metadata?.HttpMethod,
                Path = endpoint.Path ?? metadata?.Path,
                OperationId = metadata?.OperationId,
                RequiresBody = requiresBody,
                RequiresAuth = RequiresAuth(endpoint, metadata, orderItemMap, metadataMap) || requiresAuthFromSrs,
                RequiresAuthFromSrs = requiresAuthFromSrs,
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

    private static bool HasSrsAuthRequirement(
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata,
        IReadOnlyList<SrsRequirement> srsRequirements)
    {
        if (IsAuthLikeEndpoint(endpoint, metadata) || srsRequirements == null || srsRequirements.Count == 0)
        {
            return false;
        }

        return srsRequirements.Any(requirement =>
            IsSrsRequirementRelevantToEndpoint(requirement, endpoint, metadata) &&
            RequirementMentionsAuth(requirement));
    }

    private static bool HasSrsErrorExpectation(
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata,
        IReadOnlyList<SrsRequirement> srsRequirements)
    {
        if (srsRequirements == null || srsRequirements.Count == 0)
        {
            return false;
        }

        if (HasSrsAuthRequirement(endpoint, metadata, srsRequirements))
        {
            return true;
        }

        return srsRequirements.Any(requirement =>
            IsSrsRequirementRelevantToEndpoint(requirement, endpoint, metadata) &&
            ExtractStatusCodesFromRequirement(requirement).Any(status => status >= 400 && status <= 599));
    }

    private static bool IsSrsRequirementRelevantToEndpoint(
        SrsRequirement requirement,
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata)
    {
        if (requirement == null)
        {
            return false;
        }

        var endpointId = endpoint?.EndpointId ?? metadata?.EndpointId ?? Guid.Empty;
        if (endpointId != Guid.Empty && requirement.EndpointId == endpointId)
        {
            return true;
        }

        if (MatchesMappedEndpointPath(requirement.MappedEndpointPath, endpoint, metadata))
        {
            return true;
        }

        if (requirement.EndpointId.HasValue || !string.IsNullOrWhiteSpace(requirement.MappedEndpointPath))
        {
            return false;
        }

        var method = endpoint?.HttpMethod ?? metadata?.HttpMethod;
        return requirement.RequirementType == SrsRequirementType.Security &&
               IsMutationMethod(method) &&
               RequirementMentionsAuth(requirement);
    }

    private static bool MatchesMappedEndpointPath(
        string mappedEndpointPath,
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata)
    {
        if (string.IsNullOrWhiteSpace(mappedEndpointPath))
        {
            return false;
        }

        var mapped = mappedEndpointPath.Trim();
        var method = endpoint?.HttpMethod ?? metadata?.HttpMethod;
        var path = endpoint?.Path ?? metadata?.Path;
        return !string.IsNullOrWhiteSpace(method) &&
               !string.IsNullOrWhiteSpace(path) &&
               mapped.Contains(method, StringComparison.OrdinalIgnoreCase) &&
               mapped.Contains(path, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequirementMentionsAuth(SrsRequirement requirement)
    {
        if (requirement == null)
        {
            return false;
        }

        if (requirement.RequirementType == SrsRequirementType.Security)
        {
            return true;
        }

        var text = NormalizeForSearch(BuildSrsRequirementText(requirement));
        return text.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("bearer", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("jwt", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("xac thuc", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("phan quyen", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("bao mat", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("dang nhap", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<int> ExtractStatusCodesFromRequirement(SrsRequirement requirement)
    {
        var text = BuildSrsRequirementText(requirement);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<int>();
        }

        return Regex.Matches(text, @"\b[1-5][0-9]{2}\b")
            .Select(match => int.TryParse(match.Value, out var status) ? status : 0)
            .Where(status => status >= 100 && status <= 599)
            .Distinct()
            .ToList();
    }

    private static string BuildSrsRequirementText(SrsRequirement requirement)
        => string.Join(" ", new[]
        {
            requirement?.RequirementCode,
            requirement?.Title,
            requirement?.Description,
            requirement?.RequirementType.ToString(),
            requirement?.TestableConstraints,
            requirement?.RefinedConstraints,
            requirement?.MappedEndpointPath,
        });

    private static string NormalizeForSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private static bool IsMutationMethod(string method)
        => string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);

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
            .GroupBy(p => $"{p.Location?.Trim().ToLowerInvariant()}:{p.Name?.Trim().ToLowerInvariant()}")
            .Select(g => g.First())
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
                matchingResponse.Schema,
                scenario.SuggestedTestType,
                primaryCode);
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

                var field = item.TryGetProperty("field", out var fieldElement) ? ElementToCompactString(fieldElement) : null;
                var operatorName = item.TryGetProperty("operator", out var operatorElement) ? ElementToCompactString(operatorElement) : null;
                var value = item.TryGetProperty("value", out var valueElement) ? ElementToCompactString(valueElement) : null;
                var expectedStatus = item.TryGetProperty("expectedStatus", out var expectedStatusElement)
                    ? ElementToCompactString(expectedStatusElement)
                    : null;
                var expectedOutcome = item.TryGetProperty("expectedOutcome", out var expectedOutcomeElement)
                    ? ElementToCompactString(expectedOutcomeElement)
                    : null;
                var testType = item.TryGetProperty("testType", out var testTypeElement) ? ElementToCompactString(testTypeElement) : null;
                var priority = item.TryGetProperty("priority", out var p) ? p.GetString() : "Medium";
                var sourceText = item.TryGetProperty("sourceText", out var sourceTextElement) ? ElementToCompactString(sourceTextElement) : null;
                var requirementScope = item.TryGetProperty("requirementScope", out var requirementScopeElement) ? ElementToCompactString(requirementScopeElement) : null;
                var flowDependencies = item.TryGetProperty("flowDependencies", out var flowDependenciesElement) ? ElementToCompactString(flowDependenciesElement) : null;
                var negativeCases = item.TryGetProperty("negativeCases", out var negativeCasesElement) ? ElementToCompactString(negativeCasesElement) : null;
                var outcome = expectedOutcome ?? expectedStatus ?? ExtractExpectedOutcome(constraint);

                result.Add(new SrsTestableConstraintBrief
                {
                    Constraint = TruncateForPayload(constraint, 200),
                    Field = TruncateForPayload(field, 80),
                    Operator = TruncateForPayload(operatorName, 80),
                    Value = TruncateForPayload(value, 120),
                    ExpectedStatus = TruncateForPayload(expectedStatus, 40),
                    ExpectedOutcome = TruncateForPayload(outcome, 80),
                    TestType = TruncateForPayload(testType, 40),
                    Priority = priority,
                    SourceText = TruncateForPayload(sourceText, 180),
                    RequirementScope = TruncateForPayload(requirementScope, 40),
                    FlowDependencies = TruncateForPayload(flowDependencies, 240),
                    NegativeCases = TruncateForPayload(negativeCases, 240),
                });
            }
            return result.Take(5).ToList();
        }
        catch { return new List<SrsTestableConstraintBrief>(); }
    }

    private static string ElementToCompactString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.GetRawText(),
        };
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

    private string BuildCacheKey(
        LlmScenarioSuggestionContext context,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        string feedbackFingerprint)
    {
        var sb = new StringBuilder();
        sb.Append(ScenarioQualityPolicyVersion).Append(':');
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

        // Include budget policy values so count-policy changes invalidate cached suggestions.
        sb.Append(BuildScenarioBudgetOptionsSignature(_scenarioBudgetOptions));
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

    private static string BuildScenarioBudgetOptionsSignature(ScenarioGenerationBudgetOptions options)
    {
        options = ScenarioBudgetResolver.Normalize(options);

        return string.Join('|',
            options.SimpleEndpointSoftLimit,
            options.ComplexEndpointSoftLimit,
            options.DefaultHardLimitPerEndpoint,
            options.MaxScenarioBudgetPerBatch);
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
