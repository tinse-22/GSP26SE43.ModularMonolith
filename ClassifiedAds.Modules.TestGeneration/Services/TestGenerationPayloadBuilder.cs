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
using System.Text.Json;
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
        "1. Generate exactly 3 test cases per endpoint unless the contract makes one impossible: 1 HappyPath, 1 Boundary, 1 Negative.\n" +
        "2. endpointId must match the exact UUID from input; testType must be HappyPath, Boundary, or Negative.\n" +
        "3. Keep orderIndex unique, 0-based, and aligned with endpoint order and dependencies.\n" +
        "4. For unique fields (email, username, code, slug, phone, name), use {{tcUniqueId}}. Do not invent random suffixes.\n" +
        "5. Auth flow: registration uses unique email/password and extracts registeredEmail/registeredPassword from RequestBody; login reuses those variables; duplicate-email tests reuse {{registeredEmail}}.\n" +
        "6. request.body must be a serialized JSON string or null. expectation.expectedStatus must be an integer array.\n" +
        "7. HappyPath should prefer 2xx; Boundary/Negative should prefer contract-backed 4xx/401/403/404.\n" +
        "8. If srsRequirements are present, derive expectation and coveredRequirementIds from effectiveConstraints. Avoid contradicting SRS constraints.\n" +
        "9. Include mappingRationale and traceabilityScore when requirements are linked or ambiguous.";

    private const string UnifiedResponseFormatBlock =
        "=== RESPONSE FORMAT ===\n" +
        "Return ONLY raw JSON: {\"testCases\":[{\"endpointId\":\"<uuid>\",\"name\":\"<short>\",\"description\":\"<one sentence>\",\"testType\":\"HappyPath|Boundary|Negative\",\"priority\":\"Critical|High|Medium|Low\",\"orderIndex\":0,\"tags\":[\"happy-path\"],\"request\":{\"httpMethod\":\"GET|POST|PUT|DELETE|PATCH\",\"url\":\"/path\",\"headers\":{},\"pathParams\":{},\"queryParams\":{},\"bodyType\":\"None|JSON|FormData|UrlEncoded|Raw\",\"body\":\"<serialized JSON or null>\",\"timeout\":30000},\"expectation\":{\"expectedStatus\":[200],\"responseSchema\":null,\"headerChecks\":{},\"bodyContains\":[],\"bodyNotContains\":[],\"jsonPathChecks\":{},\"maxResponseTime\":null},\"variables\":[],\"coveredRequirementIds\":[],\"traceabilityScore\":0.95,\"mappingRationale\":\"<why this validates linked requirements>\"}],\"model\":\"<model>\",\"tokensUsed\":0,\"reasoning\":\"<brief>\"}";

    private static readonly JsonSerializerOptions CompactJsonOptions = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

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
        var compactEndpointBusinessContexts = CompactEndpointBusinessContexts(suite.EndpointBusinessContexts);

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
            Model = string.IsNullOrWhiteSpace(_n8nOptions.GenerationModel)
                ? "gpt-4.1-mini"
                : _n8nOptions.GenerationModel,
            MaxOutputTokens = ComputeMaxOutputTokens(appliedOrder.Count),
            PreferJsonObjectResponse = true,
            PromptConfig = BuildUnifiedPromptConfig(suite, promptByEndpointId, _n8nOptions.GenerationMaxBusinessContextLength),
            Endpoints = appliedOrder.Select(x =>
            {
                metadataByEndpointId.TryGetValue(x.EndpointId, out var metadata);
                promptByEndpointId.TryGetValue(x.EndpointId, out var prompt);
                var businessContext = string.Empty;
                if (compactEndpointBusinessContexts.TryGetValue(x.EndpointId, out var contextValue))
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
                    ParameterSchemaPayloads = CompactSchemaPayloads(metadata?.ParameterSchemaPayloads),
                    ResponseSchemaPayloads = CompactSchemaPayloads(metadata?.ResponseSchemaPayloads),
                };
            }).ToList(),
            EndpointBusinessContexts = compactEndpointBusinessContexts,
            GlobalBusinessRules = TruncateForPayload(suite.GlobalBusinessRules, _n8nOptions.GenerationMaxBusinessContextLength),
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

            var maxRequirementCount = _n8nOptions.GenerationMaxSrsRequirementCount <= 0
                ? 30
                : _n8nOptions.GenerationMaxSrsRequirementCount;
            var maxSrsFieldLength = _n8nOptions.GenerationMaxSrsFieldLength <= 0
                ? 700
                : _n8nOptions.GenerationMaxSrsFieldLength;

            payload.SrsRequirements = requirements
                .Take(maxRequirementCount)
                .Select(r => new N8nSrsRequirement
                {
                    Id = r.Id,
                    Code = r.RequirementCode,
                    Title = TruncateForPayload(r.Title, maxSrsFieldLength),
                    Description = TruncateForPayload(r.Description, maxSrsFieldLength),
                    RequirementType = r.RequirementType.ToString(),
                    EffectiveConstraints = TruncateForPayload(
                        !string.IsNullOrWhiteSpace(r.RefinedConstraints)
                            ? r.RefinedConstraints
                            : r.TestableConstraints,
                        maxSrsFieldLength),
                    Assumptions = TruncateForPayload(r.Assumptions, maxSrsFieldLength),
                    Ambiguities = TruncateForPayload(r.Ambiguities, maxSrsFieldLength),
                    ConfidenceScore = r.RefinedConfidenceScore ?? r.ConfidenceScore,
                })
                .ToList();

            _logger.LogInformation(
                "Included {Count}/{TotalCount} SRS requirements in generation payload. TestSuiteId={TestSuiteId}, SrsDocumentId={SrsDocumentId}",
                payload.SrsRequirements.Count, requirements.Count, suite.Id, suite.SrsDocumentId);
        }

        _logger.LogInformation(
            "Built test generation payload. TestSuiteId={TestSuiteId}, EndpointCount={EndpointCount}, Model={Model}, MaxOutputTokens={MaxOutputTokens}, CallbackUrl={CallbackUrl}",
            suite.Id, appliedOrder.Count, payload.Model, payload.MaxOutputTokens, callbackUrl);

        return payload;
    }

    private static N8nUnifiedPromptConfig BuildUnifiedPromptConfig(
        TestSuite suite,
        IReadOnlyDictionary<Guid, ObservationConfirmationPrompt> promptByEndpointId,
        int maxBusinessContextLength)
    {
        var systemPrompt = promptByEndpointId.Values
            .Select(x => x?.SystemPrompt)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return new N8nUnifiedPromptConfig
        {
            SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? DefaultUnifiedSystemPrompt
                : TruncateForPayload(systemPrompt, 900),
            TaskInstruction = BuildUnifiedTaskInstruction(suite, maxBusinessContextLength),
            Rules = UnifiedRulesBlock,
            ResponseFormat = UnifiedResponseFormatBlock,
        };
    }

    private static string BuildUnifiedTaskInstruction(TestSuite suite, int maxBusinessContextLength)
    {
        var suiteName = string.IsNullOrWhiteSpace(suite?.Name) ? "N/A" : suite.Name;
        var sb = new StringBuilder();
        sb.Append($"Generate mixed test cases for this ordered REST API sequence (suite: {suiteName}).");

        if (!string.IsNullOrWhiteSpace(suite?.GlobalBusinessRules))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("=== GLOBAL BUSINESS RULES ===");
            sb.Append(TruncateForPayload(suite.GlobalBusinessRules, maxBusinessContextLength));
        }

        return sb.ToString();
    }

    private Models.N8nPromptPayload BuildEndpointPromptPayload(
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
            SystemPrompt = TruncateForPayload(
                string.IsNullOrWhiteSpace(prompt?.SystemPrompt)
                    ? DefaultUnifiedSystemPrompt
                    : prompt.SystemPrompt,
                900),
            CombinedPrompt = TruncateForPayload(combinedPrompt, _n8nOptions.GenerationMaxPromptLength),
            ObservationPrompt = TruncateForPayload(
                string.IsNullOrWhiteSpace(prompt?.ObservationPrompt)
                    ? combinedPrompt
                    : prompt.ObservationPrompt,
                _n8nOptions.GenerationMaxPromptLength),
            ConfirmationPromptTemplate = TruncateForPayload(
                string.IsNullOrWhiteSpace(prompt?.ConfirmationPromptTemplate)
                    ? "Validate constraints strictly from provided API details and business rules; output only valid JSON."
                    : prompt.ConfirmationPromptTemplate,
                700),
        };
    }

    private int ComputeMaxOutputTokens(int endpointCount)
    {
        var minTokens = _n8nOptions.GenerationMinOutputTokens <= 0
            ? 2048
            : _n8nOptions.GenerationMinOutputTokens;
        var maxTokens = _n8nOptions.GenerationMaxOutputTokens <= 0
            ? 4096
            : _n8nOptions.GenerationMaxOutputTokens;
        var perEndpoint = _n8nOptions.GenerationOutputTokensPerEndpoint <= 0
            ? 768
            : _n8nOptions.GenerationOutputTokensPerEndpoint;

        var computed = minTokens + Math.Max(endpointCount - 1, 0) * perEndpoint;
        return Math.Clamp(computed, minTokens, maxTokens);
    }

    private Dictionary<Guid, string> CompactEndpointBusinessContexts(IReadOnlyDictionary<Guid, string> contexts)
    {
        if (contexts == null || contexts.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return contexts
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(
                x => x.Key,
                x => TruncateForPayload(x.Value, _n8nOptions.GenerationMaxBusinessContextLength));
    }

    private List<string> CompactSchemaPayloads(IEnumerable<string> schemaPayloads)
    {
        if (schemaPayloads == null)
        {
            return new List<string>();
        }

        var maxCount = _n8nOptions.GenerationMaxSchemaPayloadCountPerKind;
        if (maxCount <= 0)
        {
            return new List<string>();
        }

        var maxLength = _n8nOptions.GenerationMaxSchemaPayloadLength <= 0
            ? 1200
            : _n8nOptions.GenerationMaxSchemaPayloadLength;

        return schemaPayloads
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(maxCount)
            .Select(x => TruncateForPayload(CompactJsonOrText(x), maxLength))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static string CompactJsonOrText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        try
        {
            using var document = JsonDocument.Parse(trimmed);
            return JsonSerializer.Serialize(document.RootElement, CompactJsonOptions);
        }
        catch (JsonException)
        {
            return trimmed.Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }

    private static string TruncateForPayload(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (maxLength <= 0)
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
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
