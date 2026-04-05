using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class GenerateTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GenerateTestCasesCommandHandler : ICommandHandler<GenerateTestCasesCommand>
{
    private const string WebhookName = N8nWebhookNames.GenerateTestCasesUnified;
    private const string DefaultUnifiedSystemPrompt =
        "You are a senior QA engineer specializing in REST API test generation. Generate robust, executable, deterministic test cases and return ONLY raw JSON.";

    private const string UnifiedRulesBlock =
        "=== RULES ===\n" +
        "1. For EACH endpoint, generate at least: 1 HappyPath + 1 Boundary + 1 Negative.\n" +
        "2. testType must be exactly one of: \"HappyPath\", \"Boundary\", \"Negative\".\n" +
        "3. endpointId must match exact UUID from input.\n" +
        "4. Keep execution order aligned with endpoint order (orderIndex unique, 0-based, no duplicates).\n" +
        "5. Use realistic synthetic data and consistent variable names.\n" +
        "6. request.body must be stringified JSON or null.\n" +
        "7. expectation.expectedStatus must be an array of integers.\n" +
        "8. HappyPath expectedStatus should prefer 2xx. Boundary/Negative should prefer 4xx (or 401/403/404 where appropriate).\n" +
        "9. Return complete request/expectation objects for each test case.";

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
        "      \"variables\": []\n" +
        "    }\n" +
        "  ],\n" +
        "  \"model\": \"<model name>\",\n" +
        "  \"tokensUsed\": 0,\n" +
        "  \"reasoning\": \"<brief note>\"\n" +
        "}";

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IObservationConfirmationPromptBuilder _promptBuilder;
    private readonly IN8nIntegrationService _n8nService;
    private readonly IApiTestOrderService _apiTestOrderService;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly ILogger<GenerateTestCasesCommandHandler> _logger;

    public GenerateTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IApiEndpointMetadataService endpointMetadataService,
        IObservationConfirmationPromptBuilder promptBuilder,
        IN8nIntegrationService n8nService,
        IApiTestOrderService apiTestOrderService,
        IOptions<N8nIntegrationOptions> n8nOptions,
        ILogger<GenerateTestCasesCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _proposalRepository = proposalRepository;
        _endpointMetadataService = endpointMetadataService;
        _promptBuilder = promptBuilder;
        _n8nService = n8nService;
        _apiTestOrderService = apiTestOrderService;
        _n8nOptions = n8nOptions.Value;
        _logger = logger;
    }

    public async Task HandleAsync(GenerateTestCasesCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TestSuiteId == Guid.Empty)
            throw new ValidationException("TestSuiteId là bắt buộc.");

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
            throw new NotFoundException($"Không tìm thấy test suite '{command.TestSuiteId}'.");

        if (suite.CreatedById != command.CurrentUserId)
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");

        // Get the latest approved proposal to get the applied order
        var approvedProposal = await _proposalRepository.FirstOrDefaultAsync(
            _proposalRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId
                    && (x.Status == ProposalStatus.Approved
                        || x.Status == ProposalStatus.ModifiedAndApproved))
                .OrderByDescending(x => x.ProposalNumber));

        if (approvedProposal == null)
            throw new ValidationException("Cần approve test order trước khi generate test cases.");

        // Prefer AppliedOrder, fall back to UserModifiedOrder then ProposedOrder
        var appliedOrder = _apiTestOrderService.DeserializeOrderJson(approvedProposal.AppliedOrder);

        if (appliedOrder.Count == 0)
        {
            _logger.LogWarning(
                "AppliedOrder is empty for proposal {ProposalId}, falling back to UserModifiedOrder/ProposedOrder. TestSuiteId={TestSuiteId}",
                approvedProposal.Id, command.TestSuiteId);

            appliedOrder = _apiTestOrderService.DeserializeOrderJson(approvedProposal.UserModifiedOrder);
        }

        if (appliedOrder.Count == 0)
        {
            appliedOrder = _apiTestOrderService.DeserializeOrderJson(approvedProposal.ProposedOrder);
        }

        if (appliedOrder.Count == 0)
        {
            throw new ValidationException("Thứ tự test endpoints rỗng. Vui lòng kiểm tra lại proposal và thử approve lại.");
        }

        _logger.LogInformation(
            "Generating tests for TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, EndpointCount={EndpointCount}",
            command.TestSuiteId, approvedProposal.Id, appliedOrder.Count);

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
                    DependsOnEndpointIds = x.DependsOnEndpointIds?.ToList() ?? new(),
                    ReasonCodes = x.ReasonCodes?.ToList() ?? new(),
                    IsAuthRelated = x.IsAuthRelated,
                    BusinessContext = businessContext,
                    Prompt = BuildEndpointPromptPayload(x, suite, metadata, businessContext, prompt),
                    ParameterSchemaPayloads = metadata?.ParameterSchemaPayloads?.ToList() ?? new(),
                    ResponseSchemaPayloads = metadata?.ResponseSchemaPayloads?.ToList() ?? new(),
                };
            }).ToList(),
            EndpointBusinessContexts = suite.EndpointBusinessContexts ?? new(),
            GlobalBusinessRules = suite.GlobalBusinessRules,
            CallbackUrl = callbackUrl,
            CallbackApiKey = callbackApiKey,
        };

        await _n8nService.TriggerWebhookAsync(WebhookName, payload, cancellationToken);

        _logger.LogInformation(
            "Triggered test generation via n8n. TestSuiteId={TestSuiteId}, EndpointCount={EndpointCount}, ActorUserId={ActorUserId}",
            suite.Id, appliedOrder.Count, command.CurrentUserId);
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

public class N8nGenerateTestsPayload
{
    public Guid TestSuiteId { get; set; }
    public string TestSuiteName { get; set; } = string.Empty;
    public Guid SpecificationId { get; set; }
    public N8nUnifiedPromptConfig PromptConfig { get; set; } = new();
    public List<N8nOrderedEndpoint> Endpoints { get; set; } = new();
    public Dictionary<Guid, string> EndpointBusinessContexts { get; set; } = new();
    public string GlobalBusinessRules { get; set; }
    /// <summary>BE endpoint n8n should POST generated test cases back to.</summary>
    public string CallbackUrl { get; set; } = string.Empty;
    /// <summary>Sent back by n8n in the x-callback-api-key header so BE can validate it.</summary>
    public string CallbackApiKey { get; set; } = string.Empty;
}

public class N8nOrderedEndpoint
{
    public Guid EndpointId { get; set; }
    public string HttpMethod { get; set; }
    public string Path { get; set; }
    public string OperationId { get; set; }
    public int OrderIndex { get; set; }
    public List<Guid> DependsOnEndpointIds { get; set; } = new();
    public List<string> ReasonCodes { get; set; } = new();
    public bool IsAuthRelated { get; set; }
    public string BusinessContext { get; set; }
    public N8nPromptPayload Prompt { get; set; }
    public List<string> ParameterSchemaPayloads { get; set; } = new();
    public List<string> ResponseSchemaPayloads { get; set; } = new();
}

public class N8nPromptPayload
{
    public string SystemPrompt { get; set; }
    public string CombinedPrompt { get; set; }
    public string ObservationPrompt { get; set; }
    public string ConfirmationPromptTemplate { get; set; }
}

public class N8nUnifiedPromptConfig
{
    public string SystemPrompt { get; set; }
    public string TaskInstruction { get; set; }
    public string Rules { get; set; }
    public string ResponseFormat { get; set; }
}
