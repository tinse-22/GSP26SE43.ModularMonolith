using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class GenerateLlmSuggestionPreviewCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public Guid SpecificationId { get; set; }
    public bool ForceRefresh { get; set; }
    public GenerationAlgorithmProfile AlgorithmProfile { get; set; } = new();
    public GenerateLlmSuggestionPreviewResultModel Result { get; set; }
}

public class GenerateLlmSuggestionPreviewCommandHandler : ICommandHandler<GenerateLlmSuggestionPreviewCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<SrsRequirement, Guid> _srsRequirementRepository;
    private readonly IApiTestOrderGateService _gateService;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IApiEndpointParameterDetailService _endpointParameterDetailService;
    private readonly ILlmScenarioSuggester _llmSuggester;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly ILogger<GenerateLlmSuggestionPreviewCommandHandler> _logger;

    public GenerateLlmSuggestionPreviewCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<SrsRequirement, Guid> srsRequirementRepository,
        IApiTestOrderGateService gateService,
        IApiEndpointMetadataService endpointMetadataService,
        IApiEndpointParameterDetailService endpointParameterDetailService,
        ILlmScenarioSuggester llmSuggester,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        ILogger<GenerateLlmSuggestionPreviewCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
        _srsDocumentRepository = srsDocumentRepository;
        _srsRequirementRepository = srsRequirementRepository;
        _gateService = gateService;
        _endpointMetadataService = endpointMetadataService;
        _endpointParameterDetailService = endpointParameterDetailService;
        _llmSuggester = llmSuggester;
        _subscriptionLimitService = subscriptionLimitService;
        _logger = logger;
    }

    public async Task HandleAsync(
        GenerateLlmSuggestionPreviewCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1) Validate inputs
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId là bắt buộc.");
        ValidationException.Requires(command.SpecificationId != Guid.Empty, "SpecificationId là bắt buộc.");

        // 2) Load suite + ownership
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");
        }

        ValidationException.Requires(
            suite.CreatedById == command.CurrentUserId,
            "Bạn không phải chủ sở hữu của test suite này.");

        ValidationException.Requires(
            suite.Status != TestSuiteStatus.Archived,
            "Không thể tạo suggestion preview cho test suite đã archived.");

        // 3) Gate: require approved order
        var approvedOrder = await _gateService.RequireApprovedOrderAsync(command.TestSuiteId, cancellationToken);

        // 4) Check LLM usage limit
        var llmLimitCheck = await _subscriptionLimitService.CheckLimitAsync(
            command.CurrentUserId, LimitType.MaxLlmCallsPerMonth, 1, cancellationToken);

        if (!llmLimitCheck.IsAllowed)
        {
            throw new ValidationException(
                $"Đã vượt quá giới hạn LLM calls cho gói subscription. {llmLimitCheck.DenialReason}");
        }

        // 5) Check existing pending suggestions
        if (!command.ForceRefresh)
        {
            var existingPending = await _suggestionRepository.ToListAsync(
                _suggestionRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == command.TestSuiteId
                        && x.ReviewStatus == ReviewStatus.Pending));

            ValidationException.Requires(
                existingPending.Count == 0,
                "Đã có suggestion preview đang chờ review. Sử dụng ForceRefresh=true để tạo mới.");
        }

        // 6) Build contract-rich LLM context (metadata + parameter details)
        var endpointIds = approvedOrder
            .Select(x => x.EndpointId)
            .Distinct()
            .ToList();

        var endpointMetadata = await _endpointMetadataService.GetEndpointMetadataAsync(
            command.SpecificationId,
            endpointIds,
            cancellationToken);

        var endpointParameterDetails = await _endpointParameterDetailService.GetParameterDetailsAsync(
            command.SpecificationId,
            endpointIds,
            cancellationToken);

        // 6a) Load SRS document + requirements when available so LLM can generate traceable scenarios
        SrsDocument srsDocument = null;
        List<SrsRequirement> srsRequirements = new();

        if (suite.SrsDocumentId.HasValue)
        {
            srsDocument = await _srsDocumentRepository.FirstOrDefaultAsync(
                _srsDocumentRepository.GetQueryableSet()
                    .Where(x => x.Id == suite.SrsDocumentId.Value));

            if (srsDocument != null)
            {
                srsRequirements = await _srsRequirementRepository.ToListAsync(
                    _srsRequirementRepository.GetQueryableSet()
                        .Where(x => x.SrsDocumentId == srsDocument.Id));
            }
        }

        var llmContext = new LlmScenarioSuggestionContext
        {
            TestSuiteId = suite.Id,
            UserId = command.CurrentUserId,
            Suite = suite,
            EndpointMetadata = endpointMetadata,
            OrderedEndpoints = approvedOrder,
            SpecificationId = command.SpecificationId,
            EndpointParameterDetails = endpointParameterDetails.ToDictionary(x => x.EndpointId),
            AlgorithmProfile = command.AlgorithmProfile ?? new GenerationAlgorithmProfile(),
            BypassCache = command.ForceRefresh,
            SrsDocument = srsDocument,
            SrsRequirements = srsRequirements,
        };

        var llmResult = await _llmSuggester.SuggestScenariosAsync(llmContext, cancellationToken);

        if (llmResult.Scenarios.Count == 0)
        {
            command.Result = new GenerateLlmSuggestionPreviewResultModel
            {
                TestSuiteId = command.TestSuiteId,
                TotalSuggestions = 0,
                EndpointsCovered = 0,
                LlmModel = llmResult.LlmModel,
                LlmTokensUsed = llmResult.TokensUsed,
                FromCache = llmResult.FromCache,
                GeneratedAt = DateTimeOffset.UtcNow,
                Suggestions = new List<LlmSuggestionModel>(),
            };
            return;
        }

        // 7) Supersede existing non-materialized suggestions (treat new generate as active batch)
        var existingSuggestions = await _suggestionRepository.ToListAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId
                    && !x.AppliedTestCaseId.HasValue));

        var now = DateTimeOffset.UtcNow;

        foreach (var existing in existingSuggestions)
        {
            existing.ReviewStatus = ReviewStatus.Superseded;
            existing.ReviewedById = command.CurrentUserId;
            existing.ReviewedAt = now;
            existing.UpdatedDateTime = now;
            existing.RowVersion = Guid.NewGuid().ToByteArray();
            await _suggestionRepository.UpdateAsync(existing, cancellationToken);
        }

        // 8) Persist new LlmSuggestion rows
        var suggestions = new List<LlmSuggestion>();
        var orderItemMap = approvedOrder.ToDictionary(e => e.EndpointId);
        int displayOrder = 0;

        foreach (var scenario in llmResult.Scenarios)
        {
            orderItemMap.TryGetValue(scenario.EndpointId, out var orderItem);

            var suggestion = new LlmSuggestion
            {
                Id = Guid.NewGuid(),
                TestSuiteId = suite.Id,
                EndpointId = scenario.EndpointId,
                CacheKey = null,
                DisplayOrder = displayOrder++,
                SuggestionType = scenario.SuggestedTestType == TestType.HappyPath
                    ? LlmSuggestionType.HappyPath
                    : LlmSuggestionType.BoundaryNegative,
                TestType = scenario.SuggestedTestType,
                SuggestedName = LlmSuggestionMaterializer.SanitizeName(scenario.ScenarioName, orderItem),
                SuggestedDescription = scenario.Description,
                SuggestedRequest = JsonSerializer.Serialize(new N8nTestCaseRequest
                {
                    HttpMethod = orderItem?.HttpMethod,
                    Url = orderItem?.Path,
                    BodyType = scenario.SuggestedBodyType,
                    Body = scenario.SuggestedBody,
                    PathParams = scenario.SuggestedPathParams,
                    QueryParams = scenario.SuggestedQueryParams,
                    Headers = scenario.SuggestedHeaders,
                }, JsonOpts),
                SuggestedExpectation = JsonSerializer.Serialize(new N8nTestCaseExpectation
                {
                    ExpectedStatus = scenario.GetEffectiveExpectedStatusCodes(),
                    BodyContains = scenario.SuggestedBodyContains?.Count > 0
                        ? scenario.SuggestedBodyContains
                        : (scenario.SuggestedTestType == TestType.HappyPath && !string.IsNullOrWhiteSpace(scenario.ExpectedBehavior)
                            ? new List<string> { scenario.ExpectedBehavior }
                            : new List<string>()),
                    BodyNotContains = scenario.SuggestedBodyNotContains ?? new List<string>(),
                    JsonPathChecks = NormalizeJsonPathChecks(scenario.SuggestedJsonPathChecks, scenario.SuggestedTestType),
                    HeaderChecks = scenario.SuggestedHeaderChecks ?? new Dictionary<string, string>(),
                }, JsonOpts),
                SuggestedVariables = scenario.Variables?.Count > 0
                    ? JsonSerializer.Serialize(scenario.Variables, JsonOpts)
                    : null,
                SuggestedTags = LlmSuggestionMaterializer.SerializeTags(
                    scenario.SuggestedTestType, "llm-suggested",
                    scenario.Tags?.ToArray() ?? Array.Empty<string>()),
                Priority = LlmSuggestionMaterializer.ParsePriority(scenario.Priority),
                ReviewStatus = ReviewStatus.Pending,
                LlmModel = llmResult.LlmModel,
                TokensUsed = llmResult.TokensUsed,
                SrsDocumentId = suite.SrsDocumentId,
                CoveredRequirementIds = scenario.CoveredRequirementIds?.Count > 0
                    ? JsonSerializer.Serialize(scenario.CoveredRequirementIds, JsonOpts)
                    : null,
                CreatedDateTime = now,
                RowVersion = Guid.NewGuid().ToByteArray(),
            };

            suggestions.Add(suggestion);
            await _suggestionRepository.AddAsync(suggestion, cancellationToken);
        }

        await _suggestionRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // 9) Increment LLM usage (only if live call, not cache hit)
        if (!llmResult.FromCache && !llmResult.UsedLocalFallback)
        {
            await _subscriptionLimitService.IncrementUsageAsync(
                new Contracts.Subscription.DTOs.IncrementUsageRequest
                {
                    UserId = command.CurrentUserId,
                    LimitType = LimitType.MaxLlmCallsPerMonth,
                    IncrementValue = 1,
                },
                cancellationToken);
        }

        // 10) Build result
        command.Result = new GenerateLlmSuggestionPreviewResultModel
        {
            TestSuiteId = command.TestSuiteId,
            TotalSuggestions = suggestions.Count,
            EndpointsCovered = suggestions
                .Where(s => s.EndpointId.HasValue)
                .Select(s => s.EndpointId.Value)
                .Distinct()
                .Count(),
            LlmModel = llmResult.LlmModel,
            LlmTokensUsed = llmResult.TokensUsed,
            FromCache = llmResult.FromCache,
            GeneratedAt = now,
            Suggestions = suggestions.Select(LlmSuggestionModel.FromEntity).ToList(),
        };

        _logger.LogInformation(
            "LLM suggestion preview generated. TestSuiteId={TestSuiteId}, TotalSuggestions={Total}, " +
            "EndpointsCovered={Covered}, FromCache={FromCache}, ActorUserId={UserId}",
            command.TestSuiteId, suggestions.Count,
            command.Result.EndpointsCovered, llmResult.FromCache, command.CurrentUserId);
    }

    /// <summary>
    /// For HappyPath tests, replaces hardcoded email-like values in JSONPath checks with "*" (wildcard).
    /// This prevents false failures when the test uses a dynamically generated email at runtime
    /// that differs from the static email the LLM hardcoded in the assertion.
    /// </summary>
    private static Dictionary<string, string> NormalizeJsonPathChecks(
        Dictionary<string, string> checks,
        TestType testType)
    {
        if (checks == null || checks.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        if (testType != TestType.HappyPath)
        {
            return checks;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, value) in checks)
        {
            // Replace hardcoded email values with wildcard so dynamic per-run emails don't cause false failures
            var normalized = !string.IsNullOrWhiteSpace(value) && value.Contains('@') && value != "*"
                ? "*"
                : value;
            result[path] = normalized;
        }

        return result;
    }
}
