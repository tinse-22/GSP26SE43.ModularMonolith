using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Builds the payload for n8n test generation webhook.
/// Extracted from GenerateTestCasesCommandHandler for reuse in background consumer.
/// </summary>
public interface ITestGenerationPayloadBuilder
{
    /// <summary>
    /// Builds the n8n webhook payload for a test suite.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<N8nGenerateTestsPayload> BuildPayloadAsync(
        Guid testSuiteId,
        Guid proposalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the webhook name for test generation.
    /// </summary>
    string WebhookName { get; }
}

public class TestGenerationPayloadBuilder : ITestGenerationPayloadBuilder
{
    private const string DefaultUnifiedSystemPrompt =
        "You are a senior QA engineer specializing in REST API test generation. Generate robust, executable, deterministic test cases and return ONLY raw JSON.";

    private const string UnifiedRulesBlock =
        "=== RULES ===\n" +
        "1. For EACH endpoint, generate at least: 1 HappyPath + 1 Boundary + 1 Negative.\n" +
        "2. testType must be exactly one of: \"HappyPath\", \"Boundary\", \"Negative\".\n" +
        "3. endpointId must match exact UUID from input.\n" +
        "4. Keep execution order aligned with endpoint order (orderIndex unique, 0-based, no duplicates).\n" +
        "5. Use UNIQUE synthetic data for every generation: emails MUST include a random 4-char suffix (e.g. \"testuser_a3x7@example.com\"). NEVER reuse generic emails like \"test@example.com\".\n" +
        "   AUTH FLOW RULES:\n" +
        "   - Registration HappyPath: use a unique email, add variable extraction rules to capture the email and password used (variableName: \"registeredEmail\", \"registeredPassword\", extractFrom: \"RequestBody\").\n" +
        "   - Login HappyPath: use \"{{registeredEmail}}\" and \"{{registeredPassword}}\" from the registration step so the chain works when no email confirmation is required.\n" +
        "   - If the execution environment provides {{testEmail}} and {{testPassword}}, those override for pre-confirmed accounts (users who need email confirmation can set these).\n" +
        "6. request.body must be stringified JSON or null.\n" +
        "7. expectation.expectedStatus must be an array of integers.\n" +
        "8. HappyPath expectedStatus should prefer 2xx. Boundary/Negative should prefer 4xx (or 401/403/404 where appropriate).\n" +
        "9. Return complete request/expectation objects for each test case.\n" +
        "10. If srsRequirements are provided in the input, populate coveredRequirementIds for each test case with the UUIDs of the requirements that the test case validates. Use an empty array if none apply.\n" +
        "11. SRS CONSTRAINT RULE: When srsRequirements are provided, the expectation (expectedStatus, responseSchema, bodyContains, jsonPathChecks) MUST be derived from the requirement's effectiveConstraints field. " +
        "Do NOT fabricate expectations that contradict effectiveConstraints.\n" +
        "12. CONFIDENCE + AMBIGUITY RULE: If a requirement has confidenceScore < 0.6 or a non-empty ambiguities array, include a mappingRationale on the test case explaining what assumptions were made. " +
        "Set traceabilityScore to the requirement's confidenceScore (or 0.5 if null).\n" +
        "13. TRACABILITY SCORE RULE: For well-specified requirements (confidenceScore >= 0.6, no unresolved ambiguities), set traceabilityScore = 0.9 or higher.";

    private const string UnifiedResponseFormatBlock =
        "=== RESPONSE FORMAT ===\n" +
        "Return ONLY this JSON structure:\n" +
        "{\n" +
        "  \"testCases\": [\n" +
        "    {\n" +
        "      \"endpointId\": \"<exact UUID>\",\n" +
        "      \"name\": \"<short descriptive name>\",\n" +
        "      \"description\": \"<one sentence>\",\n" +
        "      \"testType\": \"HappyPath|Boundary|Negative\",\n" +
        "      \"priority\": \"Critical|High|Medium|Low\",\n" +
        "      \"orderIndex\": 0,\n" +
        "      \"tags\": [\"happy-path\"],\n" +
        "      \"request\": {\n" +
        "        \"httpMethod\": \"GET|POST|PUT|DELETE|PATCH\",\n" +
        "        \"url\": \"/path\",\n" +
        "        \"headers\": {},\n" +
        "        \"pathParams\": {},\n" +
        "        \"queryParams\": {},\n" +
        "        \"bodyType\": \"None|JSON|FormData|UrlEncoded|Raw\",\n" +
        "        \"body\": \"<serialized JSON string or null>\",\n" +
        "        \"timeout\": 30000\n" +
        "      },\n" +
        "      \"expectation\": {\n" +
        "        \"expectedStatus\": [200],\n" +
        "        \"responseSchema\": null,\n" +
        "        \"headerChecks\": {},\n" +
        "        \"bodyContains\": [],\n" +
        "        \"bodyNotContains\": [],\n" +
        "        \"jsonPathChecks\": {},\n" +
        "        \"maxResponseTime\": null\n" +
        "      },\n" +
        "      \"variables\": [],\n" +
        "      \"coveredRequirementIds\": [\"<requirement-uuid-1>\", \"<requirement-uuid-2>\"],\n" +
        "      \"traceabilityScore\": 0.95,\n" +
        "      \"mappingRationale\": \"<one-sentence explanation of why this test validates the linked requirements>\"\n" +
        "    }\n" +
        "  ],\n" +
        "  \"model\": \"<model name>\",\n" +
        "  \"tokensUsed\": 0,\n" +
        "  \"reasoning\": \"<brief note>\"\n" +
        "}";

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IRepository<SrsRequirement, Guid> _srsRequirementRepository;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IObservationConfirmationPromptBuilder _promptBuilder;
    private readonly IApiTestOrderService _apiTestOrderService;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly ILogger<TestGenerationPayloadBuilder> _logger;

    public string WebhookName => N8nWebhookNames.GenerateTestCasesUnified;

    public TestGenerationPayloadBuilder(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IRepository<SrsRequirement, Guid> srsRequirementRepository,
        IApiEndpointMetadataService endpointMetadataService,
        IObservationConfirmationPromptBuilder promptBuilder,
        IApiTestOrderService apiTestOrderService,
        IOptions<N8nIntegrationOptions> n8nOptions,
        ILogger<TestGenerationPayloadBuilder> logger)
    {
        _suiteRepository = suiteRepository;
        _proposalRepository = proposalRepository;
        _srsRequirementRepository = srsRequirementRepository;
        _endpointMetadataService = endpointMetadataService;
        _promptBuilder = promptBuilder;
        _apiTestOrderService = apiTestOrderService;
        _n8nOptions = n8nOptions.Value;
        _logger = logger;
    }

    public async Task<N8nGenerateTestsPayload> BuildPayloadAsync(
        Guid testSuiteId,
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == testSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite '{testSuiteId}'.");
        }

        var proposal = await _proposalRepository.FirstOrDefaultAsync(
            _proposalRepository.GetQueryableSet().Where(x => x.Id == proposalId));

        if (proposal == null)
        {
            throw new NotFoundException($"Không tìm thấy proposal '{proposalId}'.");
        }

        // Prefer AppliedOrder, fall back to UserModifiedOrder then ProposedOrder
        var appliedOrder = _apiTestOrderService.DeserializeOrderJson(proposal.AppliedOrder);

        if (appliedOrder.Count == 0)
        {
            _logger.LogWarning(
                "AppliedOrder is empty for proposal {ProposalId}, falling back to UserModifiedOrder/ProposedOrder. TestSuiteId={TestSuiteId}",
                proposal.Id, testSuiteId);

            appliedOrder = _apiTestOrderService.DeserializeOrderJson(proposal.UserModifiedOrder);
        }

        if (appliedOrder.Count == 0)
        {
            appliedOrder = _apiTestOrderService.DeserializeOrderJson(proposal.ProposedOrder);
        }

        if (appliedOrder.Count == 0)
        {
            throw new ValidationException("Thứ tự test endpoints rỗng. Vui lòng kiểm tra lại proposal và thử approve lại.");
        }

        var callbackApiKey = _n8nOptions.CallbackApiKey ?? string.Empty;
        var beBaseUrl = _n8nOptions.BeBaseUrl?.TrimEnd('/') ?? string.Empty;
        var callbackUrl = string.IsNullOrWhiteSpace(beBaseUrl)
            ? string.Empty
            : $"{beBaseUrl}/api/test-suites/{suite.Id}/test-cases/from-ai";

        var metadataByEndpointId = new Dictionary<Guid, ApiEndpointMetadataDto>();
        var promptByEndpointId = new Dictionary<Guid, ObservationConfirmationPrompt>();

        if (suite.ApiSpecId.HasValue && suite.ApiSpecId.Value != Guid.Empty)
        {
            var endpointIds = appliedOrder.Select(x => x.EndpointId).ToList();
            var endpointMetadata = await _endpointMetadataService.GetEndpointMetadataAsync(
                suite.ApiSpecId.Value,
                endpointIds,
                cancellationToken);

            metadataByEndpointId = endpointMetadata
                .GroupBy(x => x.EndpointId)
                .Select(x => x.First())
                .ToDictionary(x => x.EndpointId);

            var orderedMetadata = appliedOrder
                .Where(x => metadataByEndpointId.ContainsKey(x.EndpointId))
                .Select(x => metadataByEndpointId[x.EndpointId])
                .ToList();

            var promptContexts = EndpointPromptContextMapper.Map(orderedMetadata, suite);
            var prompts = _promptBuilder.BuildForSequence(promptContexts);

            for (var index = 0; index < orderedMetadata.Count && index < prompts.Count; index++)
            {
                var prompt = prompts[index];
                if (prompt == null)
                {
                    continue;
                }

                promptByEndpointId[orderedMetadata[index].EndpointId] = prompt;
            }
        }

        var payload = new N8nGenerateTestsPayload
        {
            TestSuiteId = suite.Id,
            TestSuiteName = suite.Name,
            SpecificationId = suite.ApiSpecId ?? Guid.Empty,
            PromptConfig = BuildUnifiedPromptConfig(suite, promptByEndpointId),
            Endpoints = appliedOrder.Select(x =>
            {
                metadataByEndpointId.TryGetValue(x.EndpointId, out var metadata);
                promptByEndpointId.TryGetValue(x.EndpointId, out var prompt);
                var businessContext = string.Empty;
                if (suite.EndpointBusinessContexts != null
                    && suite.EndpointBusinessContexts.TryGetValue(x.EndpointId, out var contextValue))
                {
                    businessContext = contextValue;
                }

                return new N8nOrderedEndpoint
                {
                    EndpointId = x.EndpointId,
                    HttpMethod = x.HttpMethod,
                    Path = x.Path,
                    OperationId = metadata?.OperationId,
                    OrderIndex = x.OrderIndex,
                    DependsOnEndpointIds = x.DependsOnEndpointIds?.ToList() ?? new List<Guid>(),
                    ReasonCodes = x.ReasonCodes?.ToList() ?? new List<string>(),
                    IsAuthRelated = x.IsAuthRelated,
                    BusinessContext = businessContext,
                    Prompt = BuildEndpointPromptPayload(x, suite, metadata, businessContext, prompt),
                    ParameterSchemaPayloads = metadata?.ParameterSchemaPayloads?.ToList() ?? new List<string>(),
                    ResponseSchemaPayloads = metadata?.ResponseSchemaPayloads?.ToList() ?? new List<string>(),
                };
            }).ToList(),
            EndpointBusinessContexts = suite.EndpointBusinessContexts ?? new Dictionary<Guid, string>(),
            GlobalBusinessRules = suite.GlobalBusinessRules,
            CallbackUrl = callbackUrl,
            CallbackApiKey = callbackApiKey,
        };

        // Include SRS requirements so LLM can populate coveredRequirementIds.
        if (suite.SrsDocumentId.HasValue)
        {
            var requirements = await _srsRequirementRepository.ToListAsync(
                _srsRequirementRepository.GetQueryableSet()
                    .Where(x => x.SrsDocumentId == suite.SrsDocumentId.Value)
                    .OrderBy(x => x.DisplayOrder));

            payload.SrsRequirements = requirements
                .Select(r => new N8nSrsRequirement
                {
                    Id = r.Id,
                    Code = r.RequirementCode,
                    Title = r.Title,
                    Description = r.Description,
                    RequirementType = r.RequirementType.ToString(),
                    EffectiveConstraints = !string.IsNullOrWhiteSpace(r.RefinedConstraints)
                        ? r.RefinedConstraints
                        : r.TestableConstraints,
                    Assumptions = r.Assumptions,
                    Ambiguities = r.Ambiguities,
                    ConfidenceScore = r.RefinedConfidenceScore ?? r.ConfidenceScore,
                })
                .ToList();

            _logger.LogInformation(
                "Included {Count} SRS requirements in generation payload. TestSuiteId={TestSuiteId}, SrsDocumentId={SrsDocumentId}",
                payload.SrsRequirements.Count, suite.Id, suite.SrsDocumentId);
        }

        _logger.LogInformation(
            "Built test generation payload. TestSuiteId={TestSuiteId}, EndpointCount={EndpointCount}, CallbackUrl={CallbackUrl}",
            suite.Id, appliedOrder.Count, callbackUrl);

        return payload;
    }

    private static N8nUnifiedPromptConfig BuildUnifiedPromptConfig(
        TestSuite suite,
        IReadOnlyDictionary<Guid, ObservationConfirmationPrompt> promptByEndpointId)
    {
        var systemPrompt = promptByEndpointId.Values
            .Select(x => x?.SystemPrompt)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return new N8nUnifiedPromptConfig
        {
            SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? DefaultUnifiedSystemPrompt
                : systemPrompt,
            TaskInstruction = BuildUnifiedTaskInstruction(suite),
            Rules = UnifiedRulesBlock,
            ResponseFormat = UnifiedResponseFormatBlock,
        };
    }

    private static string BuildUnifiedTaskInstruction(TestSuite suite)
    {
        var suiteName = string.IsNullOrWhiteSpace(suite?.Name) ? "N/A" : suite.Name;
        var sb = new StringBuilder();
        sb.Append($"Generate mixed test cases for this ordered REST API sequence (suite: {suiteName}).");

        if (!string.IsNullOrWhiteSpace(suite?.GlobalBusinessRules))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("=== GLOBAL BUSINESS RULES ===");
            sb.Append(suite.GlobalBusinessRules);
        }

        return sb.ToString();
    }

    private static Models.N8nPromptPayload BuildEndpointPromptPayload(
        ApiOrderItemModel orderItem,
        TestSuite suite,
        ApiEndpointMetadataDto metadata,
        string businessContext,
        ObservationConfirmationPrompt prompt)
    {
        var combinedPrompt = string.IsNullOrWhiteSpace(prompt?.CombinedPrompt)
            ? BuildFallbackCombinedPrompt(orderItem, suite, metadata, businessContext)
            : prompt.CombinedPrompt;

        return new Models.N8nPromptPayload
        {
            SystemPrompt = string.IsNullOrWhiteSpace(prompt?.SystemPrompt)
                ? DefaultUnifiedSystemPrompt
                : prompt.SystemPrompt,
            CombinedPrompt = combinedPrompt,
            ObservationPrompt = string.IsNullOrWhiteSpace(prompt?.ObservationPrompt)
                ? combinedPrompt
                : prompt.ObservationPrompt,
            ConfirmationPromptTemplate = string.IsNullOrWhiteSpace(prompt?.ConfirmationPromptTemplate)
                ? "Validate constraints strictly from provided API details and business rules; output only valid JSON."
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
        sb.AppendLine("Generate mixed test cases (HappyPath, Boundary, Negative) for this endpoint.");

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
}
