using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.MessageBusMessages;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly IRepository<TestGenerationJob, Guid> _jobRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<SrsRequirement, Guid> _srsRequirementRepository;
    private readonly IApiTestOrderGateService _gateService;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IApiEndpointParameterDetailService _endpointParameterDetailService;
    private readonly ILlmScenarioSuggester _llmSuggester;
    private readonly ILlmSuggestionPreviewPersistenceService _persistenceService;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly IMessageBus _messageBus;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly ILogger<GenerateLlmSuggestionPreviewCommandHandler> _logger;

    public GenerateLlmSuggestionPreviewCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        IRepository<TestGenerationJob, Guid> jobRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<SrsRequirement, Guid> srsRequirementRepository,
        IApiTestOrderGateService gateService,
        IApiEndpointMetadataService endpointMetadataService,
        IApiEndpointParameterDetailService endpointParameterDetailService,
        ILlmScenarioSuggester llmSuggester,
        ILlmSuggestionPreviewPersistenceService persistenceService,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        IMessageBus messageBus,
        IOptions<N8nIntegrationOptions> n8nOptions,
        ILogger<GenerateLlmSuggestionPreviewCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
        _jobRepository = jobRepository;
        _srsDocumentRepository = srsDocumentRepository;
        _srsRequirementRepository = srsRequirementRepository;
        _gateService = gateService;
        _endpointMetadataService = endpointMetadataService;
        _endpointParameterDetailService = endpointParameterDetailService;
        _llmSuggester = llmSuggester;
        _persistenceService = persistenceService;
        _subscriptionLimitService = subscriptionLimitService;
        _messageBus = messageBus;
        _n8nOptions = n8nOptions?.Value ?? new N8nIntegrationOptions();
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
            BypassCache = false,
            SrsDocument = srsDocument,
            SrsRequirements = srsRequirements,
        };

        var job = new TestGenerationJob
        {
            TestSuiteId = command.TestSuiteId,
            ProposalId = null,
            Status = GenerationJobStatus.Queued,
            TriggeredById = command.CurrentUserId,
            QueuedAt = DateTimeOffset.UtcNow,
            WebhookName = N8nWebhookNames.GenerateLlmSuggestions,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

        await _jobRepository.AddAsync(job, cancellationToken);
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        var llmResult = await _llmSuggester.SuggestLocalDraftAsync(llmContext, cancellationToken);

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
                Source = "local-draft",
                RefinementStatus = ToRefinementStatus(job.Status),
                RefinementJobId = job.Id,
                GeneratedAt = DateTimeOffset.UtcNow,
                Suggestions = new List<LlmSuggestionModel>(),
            };
            await QueueRefinementAsync(job, llmContext, cancellationToken);
            return;
        }

        var suggestions = await _persistenceService.ReplacePendingSuggestionsAsync(
            suite,
            approvedOrder,
            llmResult,
            command.CurrentUserId,
            cancellationToken);

        await QueueRefinementAsync(job, llmContext, cancellationToken);

        var now = DateTimeOffset.UtcNow;
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
            Source = "local-draft",
            RefinementStatus = ToRefinementStatus(job.Status),
            RefinementJobId = job.Id,
            GeneratedAt = now,
            Suggestions = suggestions.Select(LlmSuggestionModel.FromEntity).ToList(),
        };

        _logger.LogInformation(
            "Local LLM suggestion preview generated. TestSuiteId={TestSuiteId}, TotalSuggestions={Total}, " +
            "EndpointsCovered={Covered}, RefinementJobId={RefinementJobId}, RefinementStatus={RefinementStatus}, ActorUserId={UserId}",
            command.TestSuiteId, suggestions.Count,
            command.Result.EndpointsCovered, job.Id, job.Status, command.CurrentUserId);
    }

    private async Task QueueRefinementAsync(
        TestGenerationJob job,
        LlmScenarioSuggestionContext llmContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var callbackUrl = BuildCallbackUrl(job.Id);
            var payload = await _llmSuggester.BuildAsyncRefinementPayloadAsync(
                llmContext,
                job.Id,
                callbackUrl,
                _n8nOptions.CallbackApiKey ?? string.Empty,
                cancellationToken);

            job.CallbackUrl = callbackUrl;
            job.Status = GenerationJobStatus.Queued;
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            await _messageBus.SendAsync(
                new TriggerLlmSuggestionRefinementMessage
                {
                    JobId = job.Id,
                    TestSuiteId = job.TestSuiteId,
                    TriggeredById = job.TriggeredById,
                    WebhookName = N8nWebhookNames.GenerateLlmSuggestions,
                    CallbackUrl = callbackUrl,
                    Payload = payload,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                new MetaData
                {
                    CreationDateTime = DateTimeOffset.UtcNow,
                    EnqueuedDateTime = DateTimeOffset.UtcNow,
                    MessageId = job.Id.ToString(),
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            job.Status = GenerationJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = $"Không thể queue n8n refinement: {ex.Message}";
            job.ErrorDetails = ex.ToString();
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Failed to queue async LLM suggestion refinement. JobId={JobId}, TestSuiteId={TestSuiteId}",
                job.Id,
                job.TestSuiteId);
        }
    }

    private string BuildCallbackUrl(Guid jobId)
    {
        var baseUrl = _n8nOptions.BeBaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl)
            ? $"/api/test-generation/llm-suggestions/callback/{jobId}"
            : $"{baseUrl}/api/test-generation/llm-suggestions/callback/{jobId}";
    }

    private static string ToRefinementStatus(GenerationJobStatus status) =>
        status switch
        {
            GenerationJobStatus.Completed => "succeeded",
            GenerationJobStatus.Failed => "failed",
            GenerationJobStatus.Cancelled => "cancelled",
            _ => "pending",
        };
}
