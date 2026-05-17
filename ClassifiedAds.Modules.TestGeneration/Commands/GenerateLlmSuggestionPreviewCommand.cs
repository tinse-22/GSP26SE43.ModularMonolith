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
using System.Diagnostics;
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
    public Guid JobId { get; set; }
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
        var totalStopwatch = Stopwatch.StartNew();
        var stageStopwatch = Stopwatch.StartNew();

        // 1) Validate inputs
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId là bắt buộc.");
        ValidationException.Requires(command.SpecificationId != Guid.Empty, "SpecificationId là bắt buộc.");
        var validateMs = stageStopwatch.ElapsedMilliseconds;

        // 2) Load suite + ownership
        stageStopwatch.Restart();
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == command.TestSuiteId));
        var loadSuiteMs = stageStopwatch.ElapsedMilliseconds;

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
        stageStopwatch.Restart();
        var approvedOrder = await _gateService.RequireApprovedOrderAsync(command.TestSuiteId, cancellationToken);
        var requireOrderMs = stageStopwatch.ElapsedMilliseconds;

        // 4) Check LLM usage limit
        stageStopwatch.Restart();
        var llmLimitCheck = await _subscriptionLimitService.CheckLimitAsync(
            command.CurrentUserId, LimitType.MaxLlmCallsPerMonth, 1, cancellationToken);
        var limitCheckMs = stageStopwatch.ElapsedMilliseconds;

        if (!llmLimitCheck.IsAllowed)
        {
            throw new ValidationException(
                $"Đã vượt quá giới hạn LLM calls cho gói subscription. {llmLimitCheck.DenialReason}");
        }

        // 5) Check existing pending suggestions
        stageStopwatch.Restart();
        var existingPending = await _suggestionRepository.ToListAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId
                    && x.ReviewStatus == ReviewStatus.Pending));

        if (!command.ForceRefresh)
        {
            ValidationException.Requires(
                existingPending.Count == 0,
                "Đã có suggestion preview đang chờ review. Sử dụng ForceRefresh=true để tạo mới.");
        }
        else
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var existing in existingPending)
            {
                existing.ReviewStatus = ReviewStatus.Superseded;
                existing.ReviewedById = command.CurrentUserId;
                existing.ReviewedAt = now;
                existing.UpdatedDateTime = now;
                existing.RowVersion = Guid.NewGuid().ToByteArray();
                await _suggestionRepository.UpdateAsync(existing, cancellationToken);
            }

            if (existingPending.Count > 0)
            {
                await _suggestionRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        var pendingSuggestionsCheckMs = stageStopwatch.ElapsedMilliseconds;

        // 6) Build contract-rich LLM context (metadata + parameter details)
        var endpointIds = approvedOrder
            .Select(x => x.EndpointId)
            .Distinct()
            .ToList();

        stageStopwatch.Restart();
        var endpointMetadata = await _endpointMetadataService.GetEndpointMetadataAsync(
            command.SpecificationId,
            endpointIds,
            cancellationToken);
        var metadataMs = stageStopwatch.ElapsedMilliseconds;

        stageStopwatch.Restart();
        var endpointParameterDetails = await _endpointParameterDetailService.GetParameterDetailsAsync(
            command.SpecificationId,
            endpointIds,
            cancellationToken);
        var parameterDetailMs = stageStopwatch.ElapsedMilliseconds;

        // 6a) Load SRS document + requirements when available so LLM can generate traceable scenarios
        stageStopwatch.Restart();
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

        var srsLoadMs = stageStopwatch.ElapsedMilliseconds;

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

        command.JobId = job.Id;
        var queueMetrics = await QueueRefinementAsync(job, llmContext, cancellationToken);
        totalStopwatch.Stop();

        _logger.LogInformation(
            "LLM suggestion generation queue metrics. JobId={JobId}, TestSuiteId={TestSuiteId}, BatchId={BatchId}, RefinementStatus={RefinementStatus}, ActorUserId={UserId}, EndpointCount={EndpointCount}, ValidateMs={ValidateMs}, LoadSuiteMs={LoadSuiteMs}, RequireOrderMs={RequireOrderMs}, LimitCheckMs={LimitCheckMs}, PendingSuggestionsCheckMs={PendingSuggestionsCheckMs}, MetadataMs={MetadataMs}, ParameterDetailMs={ParameterDetailMs}, SrsLoadMs={SrsLoadMs}, BuildPayloadMs={BuildPayloadMs}, PayloadBytes={PayloadBytes}, EnqueueMessageMs={EnqueueMessageMs}, TotalApiMs={TotalApiMs}, CacheHitCount={CacheHitCount}, CacheMissCount={CacheMissCount}",
            job.Id,
            command.TestSuiteId,
            job.Id,
            job.Status,
            command.CurrentUserId,
            endpointIds.Count,
            validateMs,
            loadSuiteMs,
            requireOrderMs,
            limitCheckMs,
            pendingSuggestionsCheckMs,
            metadataMs,
            parameterDetailMs,
            srsLoadMs,
            queueMetrics.BuildPayloadMs,
            queueMetrics.PayloadBytes,
            queueMetrics.EnqueueMessageMs,
            totalStopwatch.ElapsedMilliseconds,
            0,
            endpointIds.Count);
    }

    private async Task<QueueRefinementMetrics> QueueRefinementAsync(
        TestGenerationJob job,
        LlmScenarioSuggestionContext llmContext,
        CancellationToken cancellationToken)
    {
        var metrics = new QueueRefinementMetrics();

        try
        {
            var callbackUrl = BuildCallbackUrl(job.Id);
            var buildPayloadStopwatch = Stopwatch.StartNew();
            var payload = await _llmSuggester.BuildAsyncRefinementPayloadAsync(
                llmContext,
                job.Id,
                callbackUrl,
                _n8nOptions.CallbackApiKey ?? string.Empty,
                cancellationToken);
            buildPayloadStopwatch.Stop();
            metrics.BuildPayloadMs = buildPayloadStopwatch.ElapsedMilliseconds;
            metrics.PayloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload).Length;

            job.CallbackUrl = callbackUrl;
            job.Status = GenerationJobStatus.Queued;
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            var enqueueStopwatch = Stopwatch.StartNew();
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
            enqueueStopwatch.Stop();
            metrics.EnqueueMessageMs = enqueueStopwatch.ElapsedMilliseconds;
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

        return metrics;
    }

    private string BuildCallbackUrl(Guid jobId)
    {
        var baseUrl = _n8nOptions.BeBaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl)
            ? $"/api/test-generation/llm-suggestions/callback/{jobId}"
            : $"{baseUrl}/api/test-generation/llm-suggestions/callback/{jobId}";
    }

    private sealed class QueueRefinementMetrics
    {
        public long BuildPayloadMs { get; set; }

        public long PayloadBytes { get; set; }

        public long EnqueueMessageMs { get; set; }
    }
}
